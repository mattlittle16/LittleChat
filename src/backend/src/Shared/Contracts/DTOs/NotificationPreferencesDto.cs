namespace Shared.Contracts.DTOs;

public sealed record NotificationPreferencesDto(
    bool DmSoundEnabled,
    string RoomSoundLevel,
    bool DndEnabled,
    bool BrowserNotificationsEnabled
);
