namespace Shared.Contracts.DTOs;

public record RoomDto(
    Guid Id,
    string Name,
    bool IsDm,
    int UnreadCount,
    bool HasMention,
    string? LastMessagePreview,
    DateTime CreatedAt
);
