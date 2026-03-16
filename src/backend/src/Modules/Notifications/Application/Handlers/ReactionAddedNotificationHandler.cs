using Notifications.Domain;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Notifications.Application.Handlers;

public sealed class ReactionAddedNotificationHandler : IIntegrationEventHandler<ReactionUpdatedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;
    private readonly IUserNotificationRepository _repo;

    public ReactionAddedNotificationHandler(IRealtimeNotifier notifier, IUserNotificationRepository repo)
    {
        _notifier = notifier;
        _repo = repo;
    }

    public async Task HandleAsync(ReactionUpdatedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        // Only notify on adds, not removals
        if (!evt.Added)
            return;

        // Self-reactions don't generate notifications
        if (evt.ReactorUserId == evt.AuthorUserId)
            return;

        // Message was deleted before the reaction was processed
        if (evt.AuthorUserId == Guid.Empty)
            return;

        var now = DateTime.UtcNow;
        var notification = new UserNotification(
            Id: Guid.NewGuid(),
            RecipientUserId: evt.AuthorUserId,
            Type: NotificationType.Reaction,
            MessageId: evt.MessageId,
            RoomId: evt.RoomId,
            RoomName: evt.RoomName,
            FromUserId: evt.ReactorUserId,
            FromDisplayName: evt.ReactorDisplayName,
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
            evt.AuthorUserId.ToString(),
            "NotificationReceived",
            dto,
            cancellationToken);
    }
}
