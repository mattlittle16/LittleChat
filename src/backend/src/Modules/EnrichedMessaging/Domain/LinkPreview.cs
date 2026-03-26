namespace EnrichedMessaging.Domain;

public sealed record LinkPreview(
    Guid Id,
    Guid MessageId,
    string Url,
    string? Title,
    string? Description,
    string? ThumbnailUrl,
    bool IsDismissed,
    DateTime FetchedAt
);
