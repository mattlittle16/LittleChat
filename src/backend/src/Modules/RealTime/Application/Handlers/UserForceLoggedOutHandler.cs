using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class UserForceLoggedOutHandler : IIntegrationEventHandler<UserForceLoggedOutIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public UserForceLoggedOutHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(UserForceLoggedOutIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.SendToUserAsync(
            evt.TargetUserId.ToString(),
            "ForceLogout",
            new { },
            cancellationToken);
}
