namespace Shared.Contracts.DTOs;

public record RoomDto(
    Guid Id,
    string Name,
    bool IsDm,
    int UnreadCount,
    bool HasMention,
    string? LastMessagePreview,
    DateTime CreatedAt,
    bool IsPrivate = false,
    Guid? OwnerId = null,
    bool IsProtected = false,
    int MemberCount = 0,
    // Populated for DM rooms only
    Guid? OtherUserId = null,
    string? OtherUserDisplayName = null,
    string? OtherUserAvatarUrl = null
);
