using EnrichedMessaging.Domain;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EnrichedMessaging.Infrastructure.Repositories;

public sealed class DigestRepository : IDigestRepository
{
    private readonly string _connectionString;

    public DigestRepository(IConfiguration configuration)
    {
        _connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
    }

    public async Task<IReadOnlyList<DigestMessageRaw>> GetMessagesAsync(
        Guid userId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        const string sql = @"
            WITH ranked AS (
                SELECT m.id, m.room_id, r.name AS room_name,
                       m.user_id, COALESCE(u.display_name, m.""AuthorDisplayName"", 'System') AS display_name,
                       u.avatar_url, m.content, m.message_type, m.created_at,
                       m.quoted_message_id, m.quoted_author_display_name, m.quoted_content_snapshot,
                       ROW_NUMBER() OVER (PARTITION BY m.room_id ORDER BY m.created_at DESC) AS rn
                FROM messages m
                JOIN rooms r ON r.id = m.room_id
                LEFT JOIN users u ON u.id = m.user_id
                WHERE m.created_at >= @start AND m.created_at < @end
                  AND r.is_dm = false
                  AND m.is_system = false
                  AND r.id IN (SELECT room_id FROM room_memberships WHERE user_id = @uid)
            )
            SELECT id, room_id, room_name, user_id, display_name, avatar_url, content, message_type, created_at,
                   quoted_message_id, quoted_author_display_name, quoted_content_snapshot
            FROM ranked
            WHERE rn <= 15
            ORDER BY room_name ASC, created_at ASC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("start", start);
        cmd.Parameters.AddWithValue("end", end);
        cmd.Parameters.AddWithValue("uid", userId);

        var results = new List<DigestMessageRaw>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new DigestMessageRaw(
                Id:                      reader.GetGuid(0),
                RoomId:                  reader.GetGuid(1),
                RoomName:                reader.GetString(2),
                UserId:                  reader.IsDBNull(3) ? null : reader.GetGuid(3),
                DisplayName:             reader.GetString(4),
                AvatarUrl:               reader.IsDBNull(5) ? null : reader.GetString(5),
                Content:                 reader.GetString(6),
                MessageType:             reader.GetString(7),
                CreatedAt:               reader.GetDateTime(8),
                QuotedMessageId:         reader.IsDBNull(9) ? null : reader.GetGuid(9),
                QuotedAuthorDisplayName: reader.IsDBNull(10) ? null : reader.GetString(10),
                QuotedContentSnapshot:   reader.IsDBNull(11) ? null : reader.GetString(11)
            ));
        }
        return results;
    }
}
