namespace Shared.Contracts.Events;

/// <summary>Attachment metadata carried inside MessageSentIntegrationEvent.</summary>
public sealed record AttachmentEventData(
    Guid   AttachmentId,
    string FileName,
    long   FileSize,
    string ContentType,
    bool   IsImage
);
