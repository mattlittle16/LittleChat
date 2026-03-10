namespace Messaging.Domain;

public sealed record MessageAttachment(
    Guid   Id,
    Guid   MessageId,
    string FileName,
    long   FileSize,
    string FilePath,
    string ContentType,
    bool   IsImage,
    int    DisplayOrder
);
