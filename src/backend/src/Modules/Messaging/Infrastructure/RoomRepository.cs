using Messaging.Domain;
using Messaging.Infrastructure.Persistence;
using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Messaging.Infrastructure;

public sealed class RoomRepository : IRoomRepository
{
    private readonly LittleChatDbContext _db;
    private readonly NpgsqlDataSource _dataSource;

    public RoomRepository(LittleChatDbContext db, NpgsqlDataSource dataSource)
    {
        _db = db;
        _dataSource = dataSource;
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
        // Single CTE query replacing the previous multi-subquery LINQ projection.
        // Perf: ~30-50 SQL statements per call → 1.
        // Mention detection uses LIKE '%@DisplayName%' (not full-text search) to avoid
        // false positives when the user's name appears without the @ prefix.
        const string sql = """
            WITH user_rooms AS (
                SELECT rm.room_id, rm.last_read_at
                FROM room_memberships rm
                WHERE rm.user_id = $1
            ),
            unread_counts AS (
                SELECT m.room_id,
                       COUNT(*) FILTER (WHERE m.created_at > ur.last_read_at) AS unread_count,
                       BOOL_OR(
                           m.created_at > ur.last_read_at
                           AND m.content LIKE '%@' || $2 || '%'
                       ) AS has_mention
                FROM messages m
                JOIN user_rooms ur ON ur.room_id = m.room_id
                WHERE m.expires_at > NOW()
                GROUP BY m.room_id, ur.last_read_at
            ),
            last_preview AS (
                SELECT DISTINCT ON (m.room_id) m.room_id, m.content
                FROM messages m
                JOIN user_rooms ur ON ur.room_id = m.room_id
                ORDER BY m.room_id, m.created_at DESC
            ),
            member_counts AS (
                SELECT room_id, COUNT(*) AS member_count
                FROM room_memberships
                WHERE room_id IN (SELECT room_id FROM user_rooms)
                GROUP BY room_id
            ),
            dm_partners AS (
                SELECT rm.room_id, rm.user_id AS partner_id,
                       u.display_name AS partner_name,
                       u.avatar_url   AS partner_avatar
                FROM room_memberships rm
                JOIN users u ON u.id = rm.user_id
                JOIN user_rooms ur ON ur.room_id = rm.room_id
                JOIN rooms r ON r.id = rm.room_id AND r.is_dm = TRUE
                WHERE rm.user_id != $1
            )
            SELECT
                r.id, r.name, r.is_dm, r.created_by, r.created_at,
                r.owner_id, r.visibility, r.is_protected,
                COALESCE(uc.unread_count, 0) AS unread_count,
                COALESCE(uc.has_mention, FALSE) AS has_mention,
                COALESCE(mc.member_count, 0) AS member_count,
                lp.content AS last_preview,
                dp.partner_id, dp.partner_name, dp.partner_avatar
            FROM user_rooms ur
            JOIN rooms r ON r.id = ur.room_id
            LEFT JOIN unread_counts uc ON uc.room_id = ur.room_id
            LEFT JOIN last_preview lp ON lp.room_id = ur.room_id
            LEFT JOIN member_counts mc ON mc.room_id = ur.room_id
            LEFT JOIN dm_partners dp ON dp.room_id = ur.room_id
            ORDER BY CASE WHEN r.is_dm THEN COALESCE(dp.partner_name, '') ELSE r.name END
            """;

        // Resolve the current user's display name for the mention LIKE pattern.
        // We need it as a plain string parameter; the LIKE wildcard is in the SQL itself.
        var displayName = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(displayName);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var results = new List<RoomSummary>();
        while (await reader.ReadAsync(ct))
        {
            var roomId     = reader.GetGuid(0);
            var name       = reader.GetString(1);
            var isDm       = reader.GetBoolean(2);
            var createdBy  = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
            var createdAt  = reader.GetDateTime(4);
            var ownerId    = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);
            var visibility = reader.GetString(6);
            var isProtected = reader.GetBoolean(7);
            var unreadCount = reader.GetInt64(8);
            var hasMention  = reader.GetBoolean(9);
            var memberCount = reader.GetInt64(10);
            var lastPreview = reader.IsDBNull(11) ? null : reader.GetString(11);
            var partnerId   = reader.IsDBNull(12) ? (Guid?)null : reader.GetGuid(12);
            var partnerName = reader.IsDBNull(13) ? null : reader.GetString(13);
            var partnerAvatar = reader.IsDBNull(14) ? null : reader.GetString(14);

            results.Add(new RoomSummary(
                Room: new Room(roomId, name, isDm, createdBy, createdAt,
                    OwnerId: ownerId,
                    IsPrivate: visibility == "private",
                    IsProtected: isProtected),
                UnreadCount:          (int)unreadCount,
                HasMention:           hasMention,
                LastMessagePreview:   lastPreview,
                MemberCount:          (int)memberCount,
                OtherUserId:          partnerId,
                OtherUserDisplayName: partnerName,
                OtherUserAvatarUrl:   partnerAvatar
            ));
        }

        return results;
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
