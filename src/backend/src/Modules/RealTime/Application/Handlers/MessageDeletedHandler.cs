using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class MessageDeletedHandler : IIntegrationEventHandler<MessageDeletedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public MessageDeletedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(MessageDeletedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "MessageDeleted",
            evt,
            cancellationToken);
}
