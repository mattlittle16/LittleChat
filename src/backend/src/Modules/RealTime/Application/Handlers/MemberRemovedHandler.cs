using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class MemberRemovedHandler : IIntegrationEventHandler<MemberRemovedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public MemberRemovedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(MemberRemovedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.SendToUserAsync(
            evt.RemovedUserId.ToString(),
            "RemovedFromRoom",
            evt,
            cancellationToken);
}
