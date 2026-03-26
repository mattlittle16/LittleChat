namespace EnrichedMessaging.Domain;

public sealed record BookmarkFolder(
    Guid Id,
    Guid UserId,
    string Name,
    DateTime CreatedAt,
    IReadOnlyList<Bookmark>? Bookmarks = null
);

public sealed record Bookmark(
    Guid Id,
    Guid UserId,
    Guid MessageId,
    Guid? FolderId,
    Guid RoomId,
    string RoomName,
    string AuthorDisplayName,
    string ContentPreview,
    DateTime MessageCreatedAt,
    DateTime CreatedAt,
    string? PlaceholderReason // null = intact; "message_deleted"; "room_deleted"
);
