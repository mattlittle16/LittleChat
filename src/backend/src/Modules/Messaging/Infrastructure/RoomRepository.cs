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

    public async Task<IReadOnlyList<Guid>> GetAllRoomIdsAsync(CancellationToken ct = default)
    {
        return await _db.Rooms
            .Where(r => !r.IsDm)
            .Select(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken ct = default)
    {
        return await _db.Users
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    public async Task AddMemberToAllRoomsAsync(Guid userId, IReadOnlyList<Guid> roomIds, CancellationToken ct = default)
    {
        foreach (var roomId in roomIds)
        {
            _db.RoomMemberships.Add(new RoomMembershipEntity
            {
                UserId = userId,
                RoomId = roomId,
                LastReadAt = DateTime.UtcNow,
                JoinedAt = DateTime.UtcNow,
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("23505") == true)
        {
            // Race condition: membership already exists — ignore
            _db.ChangeTracker.Clear();
        }
    }

    public async Task<Room> CreateAsync(string name, Guid createdBy, IReadOnlyList<Guid> allUserIds, CancellationToken ct = default)
    {
        var entity = new RoomEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsDm = false,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Rooms.Add(entity);

        // Bulk insert memberships for ALL existing users
        _db.RoomMemberships.AddRange(allUserIds.Select(userId => new RoomMembershipEntity
        {
            UserId = userId,
            RoomId = entity.Id,
            LastReadAt = DateTime.UtcNow,
            JoinedAt = DateTime.UtcNow,
        }));

        await _db.SaveChangesAsync(ct);

        return new Room(entity.Id, entity.Name, entity.IsDm, entity.CreatedBy, entity.CreatedAt);
    }

    public async Task<IReadOnlyList<RoomSummary>> GetForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var results = await _db.RoomMemberships
            .Where(rm => rm.UserId == userId)
            .Select(rm => new
            {
                rm.Room.Id,
                rm.Room.Name,
                rm.Room.IsDm,
                rm.Room.CreatedBy,
                rm.Room.CreatedAt,
                UnreadCount = rm.Room.Messages.Count(m => m.CreatedAt > rm.LastReadAt),
                HasMention = rm.Room.Messages.Any(m =>
                    m.CreatedAt > rm.LastReadAt &&
                    m.Content.Contains("@" + rm.User.DisplayName)),
                LastPreview = rm.Room.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                // For DMs: find the other member
                OtherUserId = rm.Room.IsDm
                    ? rm.Room.Memberships
                        .Where(other => other.UserId != userId)
                        .Select(other => (Guid?)other.UserId)
                        .FirstOrDefault()
                    : null,
                OtherUserDisplayName = rm.Room.IsDm
                    ? rm.Room.Memberships
                        .Where(other => other.UserId != userId)
                        .Select(other => other.User.DisplayName)
                        .FirstOrDefault()
                    : null,
                OtherUserAvatarUrl = rm.Room.IsDm
                    ? rm.Room.Memberships
                        .Where(other => other.UserId != userId)
                        .Select(other => other.User.AvatarUrl)
                        .FirstOrDefault()
                    : null,
            })
            .OrderBy(r => r.IsDm
                ? (r.OtherUserDisplayName ?? string.Empty)
                : r.Name)
            .ToListAsync(ct);

        return results.Select(r => new RoomSummary(
            Room: new Room(r.Id, r.Name, r.IsDm, r.CreatedBy, r.CreatedAt),
            UnreadCount: r.UnreadCount,
            HasMention: r.HasMention,
            LastMessagePreview: r.LastPreview,
            OtherUserId: r.OtherUserId,
            OtherUserDisplayName: r.OtherUserDisplayName,
            OtherUserAvatarUrl: r.OtherUserAvatarUrl
        )).ToList();
    }

    public async Task<Guid?> FindDmAsync(Guid userA, Guid userB, CancellationToken ct = default)
    {
        return await _db.Rooms
            .Where(r => r.IsDm &&
                r.Memberships.Any(m => m.UserId == userA) &&
                r.Memberships.Any(m => m.UserId == userB))
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Room> CreateDmAsync(Guid userA, Guid userB, CancellationToken ct = default)
    {
        var entity = new RoomEntity
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            IsDm = true,
            CreatedBy = userA,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Rooms.Add(entity);
        _db.RoomMemberships.AddRange([
            new RoomMembershipEntity { UserId = userA, RoomId = entity.Id, LastReadAt = DateTime.UtcNow, JoinedAt = DateTime.UtcNow },
            new RoomMembershipEntity { UserId = userB, RoomId = entity.Id, LastReadAt = DateTime.UtcNow, JoinedAt = DateTime.UtcNow },
        ]);

        await _db.SaveChangesAsync(ct);

        return new Room(entity.Id, entity.Name, entity.IsDm, entity.CreatedBy, entity.CreatedAt);
    }
}
