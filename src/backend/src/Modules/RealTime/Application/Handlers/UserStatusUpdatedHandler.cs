using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class UserStatusUpdatedHandler : IIntegrationEventHandler<UserStatusUpdatedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public UserStatusUpdatedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task HandleAsync(UserStatusUpdatedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        await _notifier.BroadcastToAllAsync(
            "UserStatusUpdated",
            new { userId = evt.UserId, emoji = evt.Emoji, text = evt.Text, color = evt.Color },
            cancellationToken);
    }
}
