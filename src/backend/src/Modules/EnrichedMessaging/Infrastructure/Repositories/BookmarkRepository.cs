using EnrichedMessaging.Domain;
using EnrichedMessaging.Infrastructure.Entities;
using Npgsql;

namespace EnrichedMessaging.Infrastructure.Repositories;

public sealed class BookmarkRepository : IBookmarkRepository
{
    private readonly EnrichedMessagingDbContext _db;
    private readonly string _connectionString;

    public BookmarkRepository(EnrichedMessagingDbContext db, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _db = db;
        _connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
    }

    public async Task<(IReadOnlyList<BookmarkFolder> Folders, IReadOnlyList<Bookmark> Bookmarks)> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Get folders
        var foldersCmd = new NpgsqlCommand(
            "SELECT id, name, created_at FROM bookmark_folders WHERE user_id = @uid ORDER BY created_at ASC", conn);
        foldersCmd.Parameters.AddWithValue("uid", userId);

        var folders = new List<BookmarkFolder>();
        await using var fr = await foldersCmd.ExecuteReaderAsync(ct);
        while (await fr.ReadAsync(ct))
            folders.Add(new BookmarkFolder(fr.GetGuid(0), userId, fr.GetString(1), fr.GetDateTime(2)));
        await fr.CloseAsync();

        // Get bookmarks with isDeleted check
        var bookmarksCmd = new NpgsqlCommand(@"
            SELECT b.id, b.message_id, b.folder_id, b.room_id, b.room_name,
                   b.author_display_name, b.content_preview, b.message_created_at, b.created_at,
                   CASE WHEN m.id IS NULL THEN 'message_deleted'
                        WHEN r.id IS NULL THEN 'room_deleted'
                        ELSE NULL END AS placeholder_reason
            FROM message_bookmarks b
            LEFT JOIN messages m ON m.id = b.message_id
            LEFT JOIN rooms r ON r.id = b.room_id
            WHERE b.user_id = @uid
            ORDER BY b.created_at DESC", conn);
        bookmarksCmd.Parameters.AddWithValue("uid", userId);

        var bookmarks = new List<Bookmark>();
        await using var br = await bookmarksCmd.ExecuteReaderAsync(ct);
        while (await br.ReadAsync(ct))
        {
            bookmarks.Add(new Bookmark(
                Id: br.GetGuid(0),
                UserId: userId,
                MessageId: br.GetGuid(1),
                FolderId: br.IsDBNull(2) ? null : br.GetGuid(2),
                RoomId: br.GetGuid(3),
                RoomName: br.GetString(4),
                AuthorDisplayName: br.GetString(5),
                ContentPreview: br.GetString(6),
                MessageCreatedAt: br.GetDateTime(7),
                CreatedAt: br.GetDateTime(8),
                PlaceholderReason: br.IsDBNull(9) ? null : br.GetString(9)
            ));
        }

        return (folders, bookmarks);
    }

    public async Task<Bookmark?> AddAsync(Guid userId, Guid messageId, Guid? folderId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Fetch message details for snapshot
        var msgCmd = new NpgsqlCommand(@"
            SELECT m.room_id, r.name AS room_name, COALESCE(u.display_name, m.""AuthorDisplayName"", 'Unknown'), LEFT(m.content, 200), m.created_at
            FROM messages m
            JOIN rooms r ON r.id = m.room_id
            LEFT JOIN users u ON u.id = m.user_id
            WHERE m.id = @mid", conn);
        msgCmd.Parameters.AddWithValue("mid", messageId);
        await using var msgReader = await msgCmd.ExecuteReaderAsync(ct);
        if (!await msgReader.ReadAsync(ct))
            return null;

        var roomId = msgReader.GetGuid(0);
        var roomName = msgReader.GetString(1);
        var authorDn = msgReader.IsDBNull(2) ? "Unknown" : msgReader.GetString(2);
        var contentPreview = msgReader.GetString(3);
        var msgCreatedAt = msgReader.GetDateTime(4);
        await msgReader.CloseAsync();

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var insCmd = new NpgsqlCommand(@"
            INSERT INTO message_bookmarks
                (id, user_id, message_id, folder_id, room_id, room_name, author_display_name, content_preview, message_created_at, created_at)
            VALUES (@id, @uid, @mid, @fid, @rid, @rn, @dn, @cp, @mca, @now)
            ON CONFLICT (user_id, message_id) DO NOTHING
            RETURNING id", conn);
        insCmd.Parameters.AddWithValue("id", id);
        insCmd.Parameters.AddWithValue("uid", userId);
        insCmd.Parameters.AddWithValue("mid", messageId);
        insCmd.Parameters.AddWithValue("fid", (object?)folderId ?? DBNull.Value);
        insCmd.Parameters.AddWithValue("rid", roomId);
        insCmd.Parameters.AddWithValue("rn", roomName);
        insCmd.Parameters.AddWithValue("dn", authorDn);
        insCmd.Parameters.AddWithValue("cp", contentPreview);
        insCmd.Parameters.AddWithValue("mca", msgCreatedAt);
        insCmd.Parameters.AddWithValue("now", now);

        await using var insReader = await insCmd.ExecuteReaderAsync(ct);
        Guid returnedId;
        if (await insReader.ReadAsync(ct))
            returnedId = insReader.GetGuid(0);
        else
        {
            // Already exists
            await insReader.CloseAsync();
            var existCmd = new NpgsqlCommand(
                "SELECT id, folder_id, message_created_at, created_at FROM message_bookmarks WHERE user_id = @uid AND message_id = @mid", conn);
            existCmd.Parameters.AddWithValue("uid", userId);
            existCmd.Parameters.AddWithValue("mid", messageId);
            await using var existReader = await existCmd.ExecuteReaderAsync(ct);
            if (!await existReader.ReadAsync(ct)) return null;
            return new Bookmark(existReader.GetGuid(0), userId, messageId,
                existReader.IsDBNull(1) ? null : existReader.GetGuid(1),
                roomId, roomName, authorDn, contentPreview,
                existReader.GetDateTime(2), existReader.GetDateTime(3), null);
        }

        return new Bookmark(returnedId, userId, messageId, folderId, roomId, roomName, authorDn, contentPreview, msgCreatedAt, now, null);
    }

    public async Task<bool> DeleteAsync(Guid bookmarkId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand("DELETE FROM message_bookmarks WHERE id = @id AND user_id = @uid", conn);
        cmd.Parameters.AddWithValue("id", bookmarkId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> UpdateFolderAsync(Guid bookmarkId, Guid userId, Guid? folderId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(
            "UPDATE message_bookmarks SET folder_id = @fid WHERE id = @id AND user_id = @uid", conn);
        cmd.Parameters.AddWithValue("fid", (object?)folderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id", bookmarkId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<BookmarkFolder?> CreateFolderAsync(Guid userId, string name, CancellationToken ct = default)
    {
        var entity = new BookmarkFolderEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
        };
        _db.BookmarkFolders.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new BookmarkFolder(entity.Id, entity.UserId, entity.Name, entity.CreatedAt);
    }

    public async Task<bool> DeleteFolderAsync(Guid folderId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand("DELETE FROM bookmark_folders WHERE id = @id AND user_id = @uid", conn);
        cmd.Parameters.AddWithValue("id", folderId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> FolderExistsAsync(Guid folderId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand("SELECT 1 FROM bookmark_folders WHERE id = @id AND user_id = @uid LIMIT 1", conn);
        cmd.Parameters.AddWithValue("id", folderId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }
}
