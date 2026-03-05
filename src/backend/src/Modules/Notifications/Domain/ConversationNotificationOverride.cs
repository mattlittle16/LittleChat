namespace Notifications.Domain;

public sealed record ConversationNotificationOverride(
    Guid UserId,
    Guid RoomId,
    string Level
);
