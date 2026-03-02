using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Notifications.Application.Handlers;

public sealed class UserMentionedHandler : IIntegrationEventHandler<MentionDetectedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public UserMentionedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(MentionDetectedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        return _notifier.SendToUserAsync(
            evt.MentionedUserId.ToString(),
            "MentionNotification",
            evt,
            cancellationToken);
    }
}
