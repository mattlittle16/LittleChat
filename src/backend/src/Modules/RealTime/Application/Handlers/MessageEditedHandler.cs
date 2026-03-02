using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class MessageEditedHandler : IIntegrationEventHandler<MessageEditedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public MessageEditedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(MessageEditedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "MessageEdited",
            evt,
            cancellationToken);
}
