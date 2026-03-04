using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class RoomDeletedHandler : IIntegrationEventHandler<RoomDeletedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public RoomDeletedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(RoomDeletedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "RoomDeleted",
            evt,
            cancellationToken);
}
