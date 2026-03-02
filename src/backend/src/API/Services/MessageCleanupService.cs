using Microsoft.EntityFrameworkCore;
using Messaging.Infrastructure.Persistence;
using Shared.Contracts.Interfaces;

namespace API.Services;

/// <summary>
/// Daily background service that hard-deletes expired messages (30-day TTL).
/// Runs at 03:00 each night. For messages with file attachments, the file is
/// deleted first; the message row is only deleted on file-delete success.
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

            // 1. Delete file-attached messages (file first, then row)
            var fileMessages = await db.Messages
                .Where(m => m.ExpiresAt < DateTime.UtcNow && m.FilePath != null)
                .Select(m => new { m.Id, m.FilePath })
                .ToListAsync(ct);

            var deletedCount = 0;
            foreach (var msg in fileMessages)
            {
                try
                {
                    await fileStorage.DeleteAsync(msg.FilePath!, ct);
                    await db.Messages.Where(m => m.Id == msg.Id).ExecuteDeleteAsync(ct);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file for message {MessageId}; skipping row delete", msg.Id);
                }
            }

            // 2. Bulk-delete plain text messages
            var textDeleted = await db.Messages
                .Where(m => m.ExpiresAt < DateTime.UtcNow && m.FilePath == null)
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
