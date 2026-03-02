namespace Shared.Contracts.DTOs;

public record AttachmentDto(
    string FileName,
    long FileSize,
    string Url  // "/api/files/{messageId}"
);
