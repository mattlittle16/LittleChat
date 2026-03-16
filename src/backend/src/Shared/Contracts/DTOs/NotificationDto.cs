namespace Shared.Contracts.DTOs;

public sealed record NotificationDto(
    Guid Id,
    Guid RecipientUserId,
    string Type,
    Guid? MessageId,
    Guid RoomId,
    string RoomName,
    Guid? FromUserId,
    string FromDisplayName,
    string ContentPreview,
    bool IsRead,
    DateTime CreatedAt,
    DateTime ExpiresAt
);
