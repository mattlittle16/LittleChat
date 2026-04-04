using Files.Application;
using Npgsql;

namespace Files.Infrastructure;

public sealed class AttachmentRepository(NpgsqlDataSource db) : IAttachmentRepository
{
    public async Task<AttachmentWithRoom?> GetAttachmentWithRoomAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            SELECT a.file_path, a.file_name, a.content_type, m.room_id
            FROM message_attachments a
            JOIN messages m ON m.id = a.message_id
            WHERE a.id = $1
            """);
        cmd.Parameters.AddWithValue(attachmentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new AttachmentWithRoom(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3));
    }

    public async Task<AttachmentInfo?> GetAttachmentAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            SELECT a.file_path, a.file_name, a.content_type
            FROM message_attachments a
            WHERE a.id = $1
            """);
        cmd.Parameters.AddWithValue(attachmentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new AttachmentInfo(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    public async Task<bool> IsUserRoomMemberAsync(Guid roomId, Guid userId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            "SELECT 1 FROM room_memberships WHERE room_id = $1 AND user_id = $2");
        cmd.Parameters.AddWithValue(roomId);
        cmd.Parameters.AddWithValue(userId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }
}
