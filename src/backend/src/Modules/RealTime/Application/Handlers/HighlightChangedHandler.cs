using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class HighlightChangedHandler : IIntegrationEventHandler<HighlightChangedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public HighlightChangedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task HandleAsync(HighlightChangedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        await _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "HighlightChanged",
            new
            {
                action = evt.Action,
                highlightId = evt.HighlightId,
                roomId = evt.RoomId,
                messageId = evt.MessageId,
                highlightedByDisplayName = evt.HighlightedByDisplayName,
                highlightedAt = evt.HighlightedAt,
                contentPreview = evt.ContentPreview,
                authorDisplayName = evt.AuthorDisplayName,
            },
            cancellationToken);
    }
}
