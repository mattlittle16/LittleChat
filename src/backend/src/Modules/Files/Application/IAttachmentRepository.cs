namespace Files.Application;

public record AttachmentInfo(string FilePath, string FileName, string ContentType);

public record AttachmentWithRoom(string FilePath, string FileName, string ContentType, Guid RoomId);

public interface IAttachmentRepository
{
    Task<AttachmentInfo?> GetAttachmentAsync(Guid attachmentId, CancellationToken ct);
    Task<AttachmentWithRoom?> GetAttachmentWithRoomAsync(Guid attachmentId, CancellationToken ct);
    Task<bool> IsUserRoomMemberAsync(Guid roomId, Guid userId, CancellationToken ct);
}
