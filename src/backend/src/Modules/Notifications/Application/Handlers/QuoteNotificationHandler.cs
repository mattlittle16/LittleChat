using Notifications.Domain;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Notifications.Application.Handlers;

public sealed class QuoteNotificationHandler : IIntegrationEventHandler<MessageQuotedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;
    private readonly IUserNotificationRepository _repo;

    public QuoteNotificationHandler(IRealtimeNotifier notifier, IUserNotificationRepository repo)
    {
        _notifier = notifier;
        _repo = repo;
    }

    public async Task HandleAsync(MessageQuotedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notification = new UserNotification(
            Id: Guid.NewGuid(),
            RecipientUserId: evt.QuotedMessageAuthorId,
            Type: NotificationType.Quote,
            MessageId: evt.MessageId,
            RoomId: evt.RoomId,
            RoomName: evt.RoomName,
            FromUserId: evt.QuoterUserId,
            FromDisplayName: evt.QuoterDisplayName,
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
            evt.QuotedMessageAuthorId.ToString(),
            "NotificationReceived",
            dto,
            cancellationToken);
    }
}
