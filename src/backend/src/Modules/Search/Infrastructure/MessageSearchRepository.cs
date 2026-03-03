using Npgsql;
using Search.Application;
using Shared.Contracts.DTOs;

namespace Search.Infrastructure;

public sealed class MessageSearchRepository : IMessageSearchRepository
{
    private readonly NpgsqlDataSource _db;

    public MessageSearchRepository(NpgsqlDataSource db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(
        Guid userId,
        string q,
        string scope,
        Guid? roomId,
        CancellationToken ct = default)
    {
        string sql;
        NpgsqlCommand cmd;

        if (scope == "room" && roomId.HasValue)
        {
            // Scoped to a single room — verify membership inline
            sql = """
                SELECT m.id, m.room_id, r.name AS room_name,
                       u.display_name, m.content, m.created_at
                FROM messages m
                JOIN rooms r ON r.id = m.room_id
                JOIN users u ON u.id = m.user_id
                WHERE m.room_id = $1
                  AND m.search_vector @@ plainto_tsquery('english', $2)
                  AND m.expires_at > NOW()
                  AND EXISTS (
                    SELECT 1 FROM room_memberships rm
                    WHERE rm.room_id = $1 AND rm.user_id = $3
                  )
                ORDER BY m.created_at DESC
                LIMIT 50
                """;
            cmd = _db.CreateCommand(sql);
            cmd.Parameters.AddWithValue(roomId.Value);
            cmd.Parameters.AddWithValue(q);
            cmd.Parameters.AddWithValue(userId);
        }
        else
        {
            // Global search — all non-DM rooms the user is a member of
            sql = """
                SELECT m.id, m.room_id, r.name AS room_name,
                       u.display_name, m.content, m.created_at
                FROM messages m
                JOIN rooms r ON r.id = m.room_id
                JOIN users u ON u.id = m.user_id
                WHERE r.is_dm = FALSE
                  AND m.search_vector @@ plainto_tsquery('english', $1)
                  AND m.expires_at > NOW()
                  AND EXISTS (
                    SELECT 1 FROM room_memberships rm
                    WHERE rm.room_id = m.room_id AND rm.user_id = $2
                  )
                ORDER BY m.created_at DESC
                LIMIT 50
                """;
            cmd = _db.CreateCommand(sql);
            cmd.Parameters.AddWithValue(q);
            cmd.Parameters.AddWithValue(userId);
        }

        await using var _ = cmd;
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var results = new List<SearchResultDto>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SearchResultDto(
                MessageId: reader.GetGuid(0),
                RoomId: reader.GetGuid(1),
                RoomName: reader.GetString(2),
                AuthorDisplayName: reader.GetString(3),
                Content: reader.GetString(4),
                CreatedAt: reader.GetDateTime(5)));
        }

        return results;
    }
}
