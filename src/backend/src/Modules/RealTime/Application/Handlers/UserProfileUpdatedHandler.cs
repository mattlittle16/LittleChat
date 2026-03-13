using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class UserProfileUpdatedHandler : IIntegrationEventHandler<UserProfileUpdatedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public UserProfileUpdatedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(UserProfileUpdatedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.BroadcastToAllAsync(
            "UserProfileUpdated",
            new { userId = evt.UserId, displayName = evt.DisplayName, profileImageUrl = evt.ProfileImageUrl },
            cancellationToken);
}
