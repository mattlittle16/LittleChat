using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using Notifications.Infrastructure.Entities;

namespace Notifications.Infrastructure;

public sealed class NotificationPreferencesRepository : INotificationPreferencesRepository
{
    private readonly NotificationsDbContext _db;

    public NotificationPreferencesRepository(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationPreferences> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var entity = await _db.NotificationPreferences
            .FirstOrDefaultAsync(e => e.UserId == userId, ct);

        if (entity is null)
            return new NotificationPreferences(userId, DmSoundEnabled: true, RoomSoundLevel: "mentions_only", DndEnabled: false, BrowserNotificationsEnabled: false);

        return new NotificationPreferences(
            entity.UserId,
            entity.DmSoundEnabled,
            entity.RoomSoundLevel,
            entity.DndEnabled,
            entity.BrowserNotificationsEnabled);
    }

    public async Task UpsertAsync(NotificationPreferences prefs, CancellationToken ct = default)
    {
        var entity = await _db.NotificationPreferences
            .FirstOrDefaultAsync(e => e.UserId == prefs.UserId, ct);

        if (entity is null)
        {
            _db.NotificationPreferences.Add(new NotificationPreferencesEntity
            {
                UserId = prefs.UserId,
                DmSoundEnabled = prefs.DmSoundEnabled,
                RoomSoundLevel = prefs.RoomSoundLevel,
                DndEnabled = prefs.DndEnabled,
                BrowserNotificationsEnabled = prefs.BrowserNotificationsEnabled,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            entity.DmSoundEnabled = prefs.DmSoundEnabled;
            entity.RoomSoundLevel = prefs.RoomSoundLevel;
            entity.DndEnabled = prefs.DndEnabled;
            entity.BrowserNotificationsEnabled = prefs.BrowserNotificationsEnabled;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
