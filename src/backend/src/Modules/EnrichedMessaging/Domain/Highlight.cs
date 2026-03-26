namespace EnrichedMessaging.Domain;

public sealed record Highlight(
    Guid Id,
    Guid RoomId,
    Guid MessageId,
    Guid HighlightedByUserId,
    string HighlightedByDisplayName,
    DateTime HighlightedAt,
    bool IsDeleted,
    string? AuthorDisplayName = null,
    string? ContentPreview = null,
    DateTime? MessageCreatedAt = null
);
