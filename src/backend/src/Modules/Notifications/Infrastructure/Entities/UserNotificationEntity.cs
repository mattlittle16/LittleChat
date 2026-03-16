namespace Notifications.Infrastructure.Entities;

public sealed class UserNotificationEntity
{
    public Guid Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? MessageId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid? FromUserId { get; set; }
    public string FromDisplayName { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
