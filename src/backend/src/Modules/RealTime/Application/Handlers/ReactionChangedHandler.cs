using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class ReactionChangedHandler : IIntegrationEventHandler<ReactionUpdatedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public ReactionChangedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(ReactionUpdatedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "ReactionUpdated",
            evt,
            cancellationToken);
}
