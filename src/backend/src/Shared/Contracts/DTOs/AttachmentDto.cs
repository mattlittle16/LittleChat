namespace Shared.Contracts.DTOs;

public record AttachmentDto(
    Guid   AttachmentId,
    string FileName,
    long   FileSize,
    string ContentType,
    bool   IsImage,
    string Url  // "/api/files/attachments/{attachmentId}"
);
