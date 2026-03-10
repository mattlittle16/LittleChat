using Microsoft.EntityFrameworkCore;
using Messaging.Infrastructure.Persistence;
using Shared.Contracts.Interfaces;

namespace API.Services;

/// <summary>
/// Daily background service that hard-deletes expired messages (30-day TTL).
/// Runs at 03:00 each night. For messages with file attachments, files are
/// deleted first (per attachment); the message row is deleted after all files succeed.
/// Constitution Principle IV: hard deletes, no soft deletes.
/// </summary>
public sealed class MessageCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MessageCleanupService> _logger;

    public MessageCleanupService(IServiceProvider services, ILogger<MessageCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun(TimeOnly.FromTimeSpan(TimeSpan.FromHours(3)));
            _logger.LogInformation("Message cleanup scheduled in {Delay:g}", delay);

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LittleChatDbContext>();
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            // 1. Load expired messages that have attachments
            var expiredWithFiles = await db.Messages
                .Where(m => m.ExpiresAt < DateTime.UtcNow)
                .Where(m => m.Attachments.Any())
                .Select(m => new
                {
                    m.Id,
                    Attachments = m.Attachments.Select(a => new { a.Id, a.FilePath }).ToList()
                })
                .ToListAsync(ct);

            var deletedCount = 0;
            foreach (var msg in expiredWithFiles)
            {
                var allDeleted = true;
                foreach (var att in msg.Attachments)
                {
                    try
                    {
                        await fileStorage.DeleteAsync(att.FilePath, ct);
                    }
                    catch (Exception ex)
                    {
                        allDeleted = false;
                        _logger.LogWarning(ex, "Failed to delete file for attachment {AttachmentId} on message {MessageId}; skipping row delete", att.Id, msg.Id);
                    }
                }

                if (allDeleted)
                {
                    await db.Messages.Where(m => m.Id == msg.Id).ExecuteDeleteAsync(ct);
                    deletedCount++;
                }
            }

            // 2. Bulk-delete expired messages without attachments
            var textDeleted = await db.Messages
                .Where(m => m.ExpiresAt < DateTime.UtcNow && !m.Attachments.Any())
                .ExecuteDeleteAsync(ct);

            _logger.LogInformation(
                "Cleanup complete: {FileMessages} file messages, {TextMessages} text messages deleted",
                deletedCount, textDeleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Message cleanup failed");
        }
    }

    private static TimeSpan TimeUntilNextRun(TimeOnly targetTime)
    {
        var now = DateTime.Now;
        var next = now.Date.Add(targetTime.ToTimeSpan());
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
