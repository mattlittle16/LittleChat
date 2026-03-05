namespace Notifications.Domain;

public sealed record NotificationPreferences(
    Guid UserId,
    bool DmSoundEnabled,
    string RoomSoundLevel,
    bool DndEnabled,
    bool BrowserNotificationsEnabled
);
