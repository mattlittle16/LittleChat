using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using Notifications.Infrastructure.Entities;

namespace Notifications.Infrastructure.Repositories;

public sealed class UserNotificationRepository : Notifications.Domain.IUserNotificationRepository
{
    private readonly NotificationsDbContext _db;

    public UserNotificationRepository(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task InsertAsync(UserNotification notification, CancellationToken ct = default)
    {
        var entity = new UserNotificationEntity
        {
            Id                = notification.Id,
            RecipientUserId   = notification.RecipientUserId,
            Type              = notification.Type,
            MessageId         = notification.MessageId,
            RoomId            = notification.RoomId,
            RoomName          = notification.RoomName,
            FromUserId        = notification.FromUserId,
            FromDisplayName   = notification.FromDisplayName,
            ContentPreview    = notification.ContentPreview,
            IsRead            = false,
            CreatedAt         = notification.CreatedAt,
            ExpiresAt         = notification.ExpiresAt,
        };
        _db.UserNotifications.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserNotification>> GetByRecipientAsync(Guid recipientUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.UserNotifications
            .Where(e => e.RecipientUserId == recipientUserId && e.ExpiresAt > now)
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .Select(e => new UserNotification(
                e.Id,
                e.RecipientUserId,
                e.Type,
                e.MessageId,
                e.RoomId,
                e.RoomName,
                e.FromUserId,
                e.FromDisplayName,
                e.ContentPreview,
                e.IsRead,
                e.CreatedAt,
                e.ExpiresAt))
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(IReadOnlyList<Guid> notificationIds, Guid recipientUserId, CancellationToken ct = default)
    {
        await _db.UserNotifications
            .Where(e => notificationIds.Contains(e.Id) && e.RecipientUserId == recipientUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsRead, true), ct);
    }

    public async Task MarkReadByRoomAsync(Guid roomId, Guid recipientUserId, CancellationToken ct = default)
    {
        await _db.UserNotifications
            .Where(e => e.RoomId == roomId && e.RecipientUserId == recipientUserId && !e.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsRead, true), ct);
    }

    public async Task MarkAllReadAsync(Guid recipientUserId, CancellationToken ct = default)
    {
        await _db.UserNotifications
            .Where(e => e.RecipientUserId == recipientUserId && !e.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsRead, true), ct);
    }
}
