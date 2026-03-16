using Notifications.Domain;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Notifications.Application.Handlers;

public sealed class TopicAlertHandler : IIntegrationEventHandler<TopicAlertIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;
    private readonly IUserNotificationRepository _repo;

    public TopicAlertHandler(IRealtimeNotifier notifier, IUserNotificationRepository repo)
    {
        _notifier = notifier;
        _repo = repo;
    }

    public async Task HandleAsync(TopicAlertIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var recipientId in evt.RecipientUserIds)
        {
            var notification = new UserNotification(
                Id: Guid.NewGuid(),
                RecipientUserId: recipientId,
                Type: NotificationType.TopicAlert,
                MessageId: evt.MessageId,
                RoomId: evt.RoomId,
                RoomName: evt.RoomName,
                FromUserId: evt.SenderUserId,
                FromDisplayName: evt.SenderDisplayName,
                ContentPreview: evt.ContentPreview,
                IsRead: false,
                CreatedAt: now,
                ExpiresAt: now.AddDays(30));

            await _repo.InsertAsync(notification, cancellationToken);

            var dto = new NotificationDto(
                notification.Id,
                notification.RecipientUserId,
                notification.Type,
                notification.MessageId,
                notification.RoomId,
                notification.RoomName,
                notification.FromUserId,
                notification.FromDisplayName,
                notification.ContentPreview,
                notification.IsRead,
                notification.CreatedAt,
                notification.ExpiresAt);

            await _notifier.SendToUserAsync(
                recipientId.ToString(),
                "NotificationReceived",
                dto,
                cancellationToken);
        }
    }
}
