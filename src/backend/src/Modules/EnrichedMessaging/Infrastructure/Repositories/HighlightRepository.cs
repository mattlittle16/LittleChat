using EnrichedMessaging.Domain;
using EnrichedMessaging.Infrastructure.Entities;
using Npgsql;

namespace EnrichedMessaging.Infrastructure.Repositories;

public sealed class HighlightRepository : IHighlightRepository
{
    private readonly EnrichedMessagingDbContext _db;
    private readonly string _connectionString;

    public HighlightRepository(EnrichedMessagingDbContext db, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _db = db;
        _connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
    }

    public async Task<IReadOnlyList<Highlight>> GetByRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT h.id, h.room_id, h.message_id, h.highlighted_by_user_id,
                   h.highlighted_by_display_name, h.highlighted_at,
                   m.id IS NULL AS is_deleted,
                   COALESCE(u.display_name, m.""AuthorDisplayName"") AS author_display_name,
                   LEFT(m.content, 200) AS content_preview,
                   m.created_at AS message_created_at
            FROM message_highlights h
            LEFT JOIN messages m ON m.id = h.message_id
            LEFT JOIN users u ON u.id = m.user_id
            WHERE h.room_id = @rid
            ORDER BY h.highlighted_at DESC";

        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("rid", roomId);

        var results = new List<Highlight>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Highlight(
                Id: reader.GetGuid(reader.GetOrdinal("id")),
                RoomId: reader.GetGuid(reader.GetOrdinal("room_id")),
                MessageId: reader.GetGuid(reader.GetOrdinal("message_id")),
                HighlightedByUserId: reader.GetGuid(reader.GetOrdinal("highlighted_by_user_id")),
                HighlightedByDisplayName: reader.GetString(reader.GetOrdinal("highlighted_by_display_name")),
                HighlightedAt: reader.GetDateTime(reader.GetOrdinal("highlighted_at")),
                IsDeleted: reader.GetBoolean(reader.GetOrdinal("is_deleted")),
                AuthorDisplayName: reader.IsDBNull(reader.GetOrdinal("author_display_name")) ? null : reader.GetString(reader.GetOrdinal("author_display_name")),
                ContentPreview: reader.IsDBNull(reader.GetOrdinal("content_preview")) ? null : reader.GetString(reader.GetOrdinal("content_preview")),
                MessageCreatedAt: reader.IsDBNull(reader.GetOrdinal("message_created_at")) ? null : reader.GetDateTime(reader.GetOrdinal("message_created_at"))
            ));
        }
        return results;
    }

    public async Task<Highlight?> AddAsync(Guid roomId, Guid messageId, Guid userId, string displayName, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var sql = @"
            INSERT INTO message_highlights (id, room_id, message_id, highlighted_by_user_id, highlighted_by_display_name, highlighted_at)
            VALUES (@id, @rid, @mid, @uid, @dn, @at)
            ON CONFLICT (room_id, message_id) DO NOTHING
            RETURNING id, highlighted_at";

        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("rid", roomId);
        cmd.Parameters.AddWithValue("mid", messageId);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("dn", displayName);
        cmd.Parameters.AddWithValue("at", now);

        Guid returnedId;
        DateTime returnedAt;

        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                returnedId = reader.GetGuid(0);
                returnedAt = reader.GetDateTime(1);
            }
            else
            {
                await reader.CloseAsync();
                var existCmd = new NpgsqlCommand(
                    "SELECT id, highlighted_at FROM message_highlights WHERE room_id = @rid AND message_id = @mid",
                    conn);
                existCmd.Parameters.AddWithValue("rid", roomId);
                existCmd.Parameters.AddWithValue("mid", messageId);
                await using var existReader = await existCmd.ExecuteReaderAsync(ct);
                if (!await existReader.ReadAsync(ct)) return null;
                returnedId = existReader.GetGuid(0);
                returnedAt = existReader.GetDateTime(1);
                // existReader disposed here (end of using block)
            }
            // reader disposed here — connection free for next command
        }

        // Fetch content preview and author for the highlight entry
        var msgCmd = new NpgsqlCommand(@"
            SELECT LEFT(m.content, 200), COALESCE(u.display_name, m.""AuthorDisplayName""), m.created_at
            FROM messages m
            LEFT JOIN users u ON u.id = m.user_id
            WHERE m.id = @mid", conn);
        msgCmd.Parameters.AddWithValue("mid", messageId);
        await using var msgReader = await msgCmd.ExecuteReaderAsync(ct);
        string? contentPreview = null;
        string? authorDisplayName = null;
        DateTime? messageCreatedAt = null;
        if (await msgReader.ReadAsync(ct))
        {
            contentPreview = msgReader.IsDBNull(0) ? null : msgReader.GetString(0);
            authorDisplayName = msgReader.IsDBNull(1) ? null : msgReader.GetString(1);
            messageCreatedAt = msgReader.IsDBNull(2) ? null : msgReader.GetDateTime(2);
        }

        return new Highlight(
            Id: returnedId,
            RoomId: roomId,
            MessageId: messageId,
            HighlightedByUserId: userId,
            HighlightedByDisplayName: displayName,
            HighlightedAt: returnedAt,
            IsDeleted: false,
            AuthorDisplayName: authorDisplayName,
            ContentPreview: contentPreview,
            MessageCreatedAt: messageCreatedAt
        );
    }

    public async Task<bool> DeleteAsync(Guid highlightId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand("DELETE FROM message_highlights WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", highlightId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(
            "SELECT 1 FROM room_memberships WHERE room_id = @rid AND user_id = @uid LIMIT 1", conn);
        cmd.Parameters.AddWithValue("rid", roomId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<bool> MessageExistsInRoomAsync(Guid messageId, Guid roomId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(
            "SELECT 1 FROM messages WHERE id = @mid AND room_id = @rid LIMIT 1", conn);
        cmd.Parameters.AddWithValue("mid", messageId);
        cmd.Parameters.AddWithValue("rid", roomId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

}
