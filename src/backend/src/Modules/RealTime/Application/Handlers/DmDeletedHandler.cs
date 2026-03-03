using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class DmDeletedHandler : IIntegrationEventHandler<DmDeletedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public DmDeletedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(DmDeletedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "DmDeleted",
            evt,
            cancellationToken);
}
