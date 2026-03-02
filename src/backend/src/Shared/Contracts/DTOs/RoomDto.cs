namespace Shared.Contracts.DTOs;

public record RoomDto(
    Guid Id,
    string Name,
    bool IsDm,
    int UnreadCount,
    bool HasMention,
    string? LastMessagePreview,
    DateTime CreatedAt,
    // Populated for DM rooms only
    Guid? OtherUserId = null,
    string? OtherUserDisplayName = null,
    string? OtherUserAvatarUrl = null
);
