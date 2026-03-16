namespace Notifications.Domain;

public interface IUserNotificationRepository
{
    Task InsertAsync(UserNotification notification, CancellationToken ct = default);
    Task<IReadOnlyList<UserNotification>> GetByRecipientAsync(Guid recipientUserId, CancellationToken ct = default);
    Task MarkReadAsync(IReadOnlyList<Guid> notificationIds, Guid recipientUserId, CancellationToken ct = default);
    Task MarkReadByRoomAsync(Guid roomId, Guid recipientUserId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid recipientUserId, CancellationToken ct = default);
}
