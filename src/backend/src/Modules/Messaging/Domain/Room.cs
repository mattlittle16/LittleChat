namespace Messaging.Domain;

public sealed record Room(
    Guid Id,
    string Name,
    bool IsDm,
    Guid? CreatedBy,
    DateTime CreatedAt
);

public sealed record RoomSummary(
    Room Room,
    int UnreadCount,
    bool HasMention,
    string? LastMessagePreview,
    // Populated for DM rooms only
    Guid? OtherUserId = null,
    string? OtherUserDisplayName = null,
    string? OtherUserAvatarUrl = null
);
