using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using Notifications.Infrastructure.Entities;

namespace Notifications.Infrastructure;

public sealed class ConversationOverrideRepository : IConversationOverrideRepository
{
    private readonly NotificationsDbContext _db;

    public ConversationOverrideRepository(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ConversationNotificationOverride>> GetAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ConversationOverrides
            .Where(e => e.UserId == userId)
            .Select(e => new ConversationNotificationOverride(e.UserId, e.RoomId, e.Level))
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(ConversationNotificationOverride @override, CancellationToken ct = default)
    {
        var entity = await _db.ConversationOverrides
            .FirstOrDefaultAsync(e => e.UserId == @override.UserId && e.RoomId == @override.RoomId, ct);

        if (entity is null)
        {
            _db.ConversationOverrides.Add(new ConversationOverrideEntity
            {
                UserId = @override.UserId,
                RoomId = @override.RoomId,
                Level = @override.Level,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            entity.Level = @override.Level;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, Guid roomId, CancellationToken ct = default)
    {
        await _db.ConversationOverrides
            .Where(e => e.UserId == userId && e.RoomId == roomId)
            .ExecuteDeleteAsync(ct);
    }
}
