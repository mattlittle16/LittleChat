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

    public async Task<Room> CreateAsync(string name, Guid createdBy, bool isPrivate = false, CancellationToken ct = default)
    {
        var entity = new RoomEntity
        {
            Id          = Guid.NewGuid(),
            Name        = name,
            IsDm        = false,
            Visibility  = isPrivate ? "private" : "public",
            OwnerId     = createdBy,
            CreatedBy   = createdBy,
            CreatedAt   = DateTime.UtcNow,
        };

        _db.Rooms.Add(entity);

        // Only the creator joins on creation
        _db.RoomMemberships.Add(new RoomMembershipEntity
        {
            UserId     = createdBy,
            RoomId     = entity.Id,
            LastReadAt = DateTime.UtcNow,
            JoinedAt   = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        return MapRoom(entity);
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
                rm.Room.OwnerId,
                IsPrivate   = rm.Room.Visibility == "private",
                rm.Room.IsProtected,
                MemberCount = rm.Room.Memberships.Count(),
                UnreadCount = rm.Room.Messages.Count(m => m.CreatedAt > rm.LastReadAt),
                HasMention  = rm.Room.Messages.Any(m =>
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
            Room: new Room(r.Id, r.Name, r.IsDm, r.CreatedBy, r.CreatedAt,
                OwnerId: r.OwnerId, IsPrivate: r.IsPrivate, IsProtected: r.IsProtected),
            UnreadCount:          r.UnreadCount,
            HasMention:           r.HasMention,
            LastMessagePreview:   r.LastPreview,
            MemberCount:          r.MemberCount,
            OtherUserId:          r.OtherUserId,
            OtherUserDisplayName: r.OtherUserDisplayName,
            OtherUserAvatarUrl:   r.OtherUserAvatarUrl
        )).ToList();
    }

    public async Task AddMemberToGeneralRoomAsync(Guid userId, CancellationToken ct = default)
    {
        var generalRoomId = await _db.Rooms
            .Where(r => r.IsProtected && !r.IsDm)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (generalRoomId is null) return;

        _db.RoomMemberships.Add(new RoomMembershipEntity
        {
            UserId     = userId,
            RoomId     = generalRoomId.Value,
            LastReadAt = DateTime.UtcNow,
            JoinedAt   = DateTime.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("23505") == true)
        {
            // Already a member — idempotent, ignore
            _db.ChangeTracker.Clear();
        }
    }

    public async Task<IReadOnlyList<RoomMember>> GetMembersAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _db.RoomMemberships
            .Where(rm => rm.RoomId == roomId)
            .Select(rm => new RoomMember(rm.UserId, rm.User.DisplayName, rm.User.AvatarUrl))
            .ToListAsync(ct);
    }

    public async Task AddMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        _db.RoomMemberships.Add(new RoomMembershipEntity
        {
            UserId     = userId,
            RoomId     = roomId,
            LastReadAt = DateTime.UtcNow,
            JoinedAt   = DateTime.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("23505") == true)
        {
            _db.ChangeTracker.Clear();
        }
    }

    public async Task RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        await _db.RoomMemberships
            .Where(rm => rm.RoomId == roomId && rm.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task SetOwnerAsync(Guid roomId, Guid newOwnerId, CancellationToken ct = default)
    {
        await _db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.OwnerId, newOwnerId), ct);
    }

    public async Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        return await _db.RoomMemberships
            .AnyAsync(rm => rm.RoomId == roomId && rm.UserId == userId, ct);
    }

    public async Task<bool> IsOwnerAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        return await _db.Rooms
            .AnyAsync(r => r.Id == roomId && r.OwnerId == userId, ct);
    }

    public async Task<Guid?> GetOwnerIdAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _db.Rooms
            .Where(r => r.Id == roomId)
            .Select(r => r.OwnerId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetMemberCountAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _db.RoomMemberships
            .CountAsync(rm => rm.RoomId == roomId, ct);
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
            Id        = Guid.NewGuid(),
            Name      = string.Empty,
            IsDm      = true,
            CreatedBy = userA,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Rooms.Add(entity);
        _db.RoomMemberships.AddRange([
            new RoomMembershipEntity { UserId = userA, RoomId = entity.Id, LastReadAt = DateTime.UtcNow, JoinedAt = DateTime.UtcNow },
            new RoomMembershipEntity { UserId = userB, RoomId = entity.Id, LastReadAt = DateTime.UtcNow, JoinedAt = DateTime.UtcNow },
        ]);

        await _db.SaveChangesAsync(ct);

        return MapRoom(entity);
    }

    public async Task<IReadOnlyList<Guid>> GetRoomIdsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.RoomMemberships
            .Where(rm => rm.UserId == userId)
            .Select(rm => rm.RoomId)
            .ToListAsync(ct);
    }

    public async Task<Room?> GetByIdAsync(Guid roomId, CancellationToken ct = default)
    {
        var entity = await _db.Rooms
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        return entity is null ? null : MapRoom(entity);
    }

    public async Task<IReadOnlyList<Guid>> GetRoomMemberIdsAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _db.RoomMemberships
            .Where(rm => rm.RoomId == roomId)
            .Select(rm => rm.UserId)
            .ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid roomId, CancellationToken ct = default)
    {
        await _db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<bool> MarkReadAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var rowsAffected = await _db.RoomMemberships
            .Where(m => m.RoomId == roomId && m.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastReadAt, DateTime.UtcNow), ct);
        return rowsAffected > 0;
    }

    public async Task<IReadOnlyList<DiscoverTopicResult>> DiscoverTopicsAsync(Guid userId, string? searchTerm, CancellationToken ct = default)
    {
        var query = _db.Rooms
            .Where(r => !r.IsDm
                && r.Visibility == "public"
                && !r.Memberships.Any(m => m.UserId == userId));

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(term));
        }

        return await query
            .OrderBy(r => r.Name)
            .Select(r => new DiscoverTopicResult(
                r.Id,
                r.Name,
                r.Memberships.Count(),
                r.CreatedAt))
            .ToListAsync(ct);
    }

    private static Room MapRoom(RoomEntity e) => new(
        e.Id, e.Name, e.IsDm, e.CreatedBy, e.CreatedAt,
        OwnerId:     e.OwnerId,
        IsPrivate:   e.Visibility == "private",
        IsProtected: e.IsProtected
    );
}
