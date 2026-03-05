namespace Notifications.Domain;

public interface INotificationPreferencesRepository
{
    Task<NotificationPreferences> GetAsync(Guid userId, CancellationToken ct = default);
    Task UpsertAsync(NotificationPreferences prefs, CancellationToken ct = default);
}
