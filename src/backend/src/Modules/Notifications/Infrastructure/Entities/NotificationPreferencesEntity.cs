namespace Notifications.Infrastructure.Entities;

public sealed class NotificationPreferencesEntity
{
    public Guid UserId { get; set; }
    public bool DmSoundEnabled { get; set; }
    public string RoomSoundLevel { get; set; } = "mentions_only";
    public bool DndEnabled { get; set; }
    public bool BrowserNotificationsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}
