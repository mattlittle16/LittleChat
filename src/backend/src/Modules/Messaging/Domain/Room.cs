namespace Messaging.Domain;

public sealed record Room(
    Guid Id,
    string Name,
    bool IsDm,
    Guid? CreatedBy,
    DateTime CreatedAt,
    Guid? OwnerId = null,
    bool IsPrivate = false,
    bool IsProtected = false
);

public sealed record RoomSummary(
    Room Room,
    int UnreadCount,
    bool HasMention,
    string? LastMessagePreview,
    int MemberCount = 0,
    // Populated for DM rooms only
    Guid? OtherUserId = null,
    string? OtherUserDisplayName = null,
    string? OtherUserAvatarUrl = null
);
