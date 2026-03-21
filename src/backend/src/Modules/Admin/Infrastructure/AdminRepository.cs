using LittleChat.Modules.Admin.Application;
using LittleChat.Modules.Admin.Application.Queries;
using Npgsql;

namespace LittleChat.Modules.Admin.Infrastructure;

public sealed class AdminRepository : IAdminRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public AdminRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<(IReadOnlyList<AdminUserDto> Items, int TotalCount)> GetUsersAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        await using var countCmd = _dataSource.CreateCommand(@"
            SELECT COUNT(*) FROM users
            WHERE ($1::text IS NULL OR display_name ILIKE '%' || $1 || '%')");
        countCmd.Parameters.AddWithValue(string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)search);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var cmd = _dataSource.CreateCommand(@"
            SELECT id, display_name, avatar_url FROM users
            WHERE ($1::text IS NULL OR display_name ILIKE '%' || $1 || '%')
            ORDER BY display_name
            LIMIT $2 OFFSET $3");
        cmd.Parameters.AddWithValue(string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)search);
        cmd.Parameters.AddWithValue(pageSize);
        cmd.Parameters.AddWithValue(offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<AdminUserDto>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(new AdminUserDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return (items, totalCount);
    }

    public async Task<AdminUserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(@"
            SELECT id, display_name, avatar_url FROM users WHERE id = $1");
        cmd.Parameters.AddWithValue(userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new AdminUserDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public async Task<(IReadOnlyList<AdminTopicDto> Items, int TotalCount)> GetTopicsAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        await using var countCmd = _dataSource.CreateCommand(@"
            SELECT COUNT(*) FROM rooms
            WHERE is_dm = false
              AND ($1::text IS NULL OR name ILIKE '%' || $1 || '%')");
        countCmd.Parameters.AddWithValue(string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)search);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var cmd = _dataSource.CreateCommand(@"
            SELECT r.id, r.name, COUNT(rm.user_id) as member_count
            FROM rooms r
            LEFT JOIN room_memberships rm ON rm.room_id = r.id
            WHERE r.is_dm = false
              AND ($1::text IS NULL OR r.name ILIKE '%' || $1 || '%')
            GROUP BY r.id, r.name
            ORDER BY r.name
            LIMIT $2 OFFSET $3");
        cmd.Parameters.AddWithValue(string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)search);
        cmd.Parameters.AddWithValue(pageSize);
        cmd.Parameters.AddWithValue(offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<AdminTopicDto>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(new AdminTopicDto(
                reader.GetGuid(0),
                reader.GetString(1),
                Convert.ToInt32(reader.GetInt64(2))));
        }

        return (items, totalCount);
    }

    public async Task<(string? TopicName, IReadOnlyList<AdminTopicMemberDto>? Members)> GetTopicMembersAsync(
        Guid topicId, CancellationToken ct = default)
    {
        // Check topic exists and is not a DM
        await using var checkCmd = _dataSource.CreateCommand(@"
            SELECT name FROM rooms WHERE id = $1 AND is_dm = false");
        checkCmd.Parameters.AddWithValue(topicId);
        var topicName = await checkCmd.ExecuteScalarAsync(ct) as string;
        if (topicName is null) return (null, null);

        await using var cmd = _dataSource.CreateCommand(@"
            SELECT u.id, u.display_name, u.avatar_url
            FROM room_memberships rm
            JOIN users u ON u.id = rm.user_id
            WHERE rm.room_id = $1
            ORDER BY u.display_name");
        cmd.Parameters.AddWithValue(topicId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var members = new List<AdminTopicMemberDto>();
        while (await reader.ReadAsync(ct))
        {
            members.Add(new AdminTopicMemberDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return (topicName, members);
    }

    public async Task<string?> GetTopicNameAsync(Guid topicId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(@"
            SELECT name FROM rooms WHERE id = $1 AND is_dm = false");
        cmd.Parameters.AddWithValue(topicId);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    public async Task<bool> IsTopicMemberAsync(Guid topicId, Guid userId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(@"
            SELECT EXISTS(SELECT 1 FROM room_memberships WHERE room_id = $1 AND user_id = $2)");
        cmd.Parameters.AddWithValue(topicId);
        cmd.Parameters.AddWithValue(userId);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task AddTopicMemberAsync(Guid topicId, Guid userId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(@"
            INSERT INTO room_memberships (room_id, user_id, joined_at, last_read_at)
            VALUES ($1, $2, NOW(), NOW())
            ON CONFLICT DO NOTHING");
        cmd.Parameters.AddWithValue(topicId);
        cmd.Parameters.AddWithValue(userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveTopicMemberAsync(Guid topicId, Guid userId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(@"
            DELETE FROM room_memberships WHERE room_id = $1 AND user_id = $2");
        cmd.Parameters.AddWithValue(topicId);
        cmd.Parameters.AddWithValue(userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> CreateTopicAsync(string name, Guid createdBy, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var cmd = _dataSource.CreateCommand(@"
            INSERT INTO rooms (id, name, is_dm, visibility, owner_id, created_by, created_at, is_protected)
            VALUES ($1, $2, false, 'public', $3, $3, NOW(), false)");
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue(createdBy);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<(string Name, bool IsProtected, bool IsDm)?> GetTopicInfoForDeleteAsync(Guid topicId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(@"
            SELECT name, is_protected, is_dm FROM rooms WHERE id = $1");
        cmd.Parameters.AddWithValue(topicId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2));
    }

    public async Task DeleteTopicAsync(Guid topicId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(@"
            DELETE FROM rooms WHERE id = $1");
        cmd.Parameters.AddWithValue(topicId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
