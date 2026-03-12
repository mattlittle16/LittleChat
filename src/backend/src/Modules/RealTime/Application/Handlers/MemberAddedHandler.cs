using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class MemberAddedHandler : IIntegrationEventHandler<MemberAddedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public MemberAddedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(MemberAddedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.SendToUserAsync(
            evt.AddedUserId.ToString(),
            "RoomMembershipChanged",
            evt,
            cancellationToken);
}
