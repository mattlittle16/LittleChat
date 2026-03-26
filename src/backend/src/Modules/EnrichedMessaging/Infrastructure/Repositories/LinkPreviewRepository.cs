using EnrichedMessaging.Domain;
using EnrichedMessaging.Infrastructure.Entities;
using Npgsql;

namespace EnrichedMessaging.Infrastructure.Repositories;

public sealed class LinkPreviewRepository : ILinkPreviewRepository
{
    private readonly EnrichedMessagingDbContext _db;
    private readonly string _connectionString;

    public LinkPreviewRepository(EnrichedMessagingDbContext db, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _db = db;
        _connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
    }

    public async Task<LinkPreview?> GetByMessageIdAsync(Guid messageId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(
            "SELECT id, message_id, url, title, description, thumbnail_url, is_dismissed, fetched_at FROM link_previews WHERE message_id = @mid",
            conn);
        cmd.Parameters.AddWithValue("mid", messageId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapLinkPreview(reader);
    }

    public async Task UpsertAsync(LinkPreview preview, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(@"
            INSERT INTO link_previews (id, message_id, url, title, description, thumbnail_url, is_dismissed, fetched_at)
            VALUES (@id, @mid, @url, @title, @desc, @thumb, @dismissed, @fetched)
            ON CONFLICT (message_id) DO UPDATE
            SET url = @url, title = @title, description = @desc,
                thumbnail_url = @thumb, is_dismissed = @dismissed, fetched_at = @fetched", conn);
        cmd.Parameters.AddWithValue("id", preview.Id);
        cmd.Parameters.AddWithValue("mid", preview.MessageId);
        cmd.Parameters.AddWithValue("url", preview.Url);
        cmd.Parameters.AddWithValue("title", (object?)preview.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)preview.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("thumb", (object?)preview.ThumbnailUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("dismissed", preview.IsDismissed);
        cmd.Parameters.AddWithValue("fetched", preview.FetchedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DismissAsync(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(@"
            UPDATE link_previews lp
            SET is_dismissed = true
            FROM messages m
            WHERE lp.message_id = m.id
              AND lp.message_id = @mid
              AND m.user_id = @uid", conn);
        cmd.Parameters.AddWithValue("mid", messageId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<Guid?> GetRoomIdByMessageAndUserAsync(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(
            "SELECT room_id FROM messages WHERE id = @mid AND user_id = @uid LIMIT 1", conn);
        cmd.Parameters.AddWithValue("mid", messageId);
        cmd.Parameters.AddWithValue("uid", userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static LinkPreview MapLinkPreview(NpgsqlDataReader reader) =>
        new(
            Id: reader.GetGuid(0),
            MessageId: reader.GetGuid(1),
            Url: reader.GetString(2),
            Title: reader.IsDBNull(3) ? null : reader.GetString(3),
            Description: reader.IsDBNull(4) ? null : reader.GetString(4),
            ThumbnailUrl: reader.IsDBNull(5) ? null : reader.GetString(5),
            IsDismissed: reader.GetBoolean(6),
            FetchedAt: reader.GetDateTime(7)
        );
}
