using Messaging.Domain;
using Messaging.Infrastructure.Persistence;
using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Infrastructure;

public sealed class RoomRepository : IRoomRepository
{
    private readonly LittleChatDbContext _db;

    public RoomRepository(LittleChatDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Guid>> GetAllRoomIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Rooms
            .Where(r => !r.IsDm)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddMemberToAllRoomsAsync(Guid userId, IReadOnlyList<Guid> roomIds, CancellationToken cancellationToken = default)
    {
        var memberships = roomIds.Select(roomId => new RoomMembershipEntity
        {
            UserId = userId,
            RoomId = roomId,
            LastReadAt = DateTime.UtcNow,
            JoinedAt = DateTime.UtcNow,
        });

        // INSERT ... ON CONFLICT DO NOTHING via EF Core
        foreach (var membership in memberships)
        {
            _db.RoomMemberships.Add(membership);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true
                                           || ex.InnerException?.Message.Contains("23505") == true)
        {
            // Race condition: membership already exists — ignore
            _db.ChangeTracker.Clear();
        }
    }
}
