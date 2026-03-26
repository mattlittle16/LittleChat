using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class LinkPreviewReadyHandler : IIntegrationEventHandler<LinkPreviewReadyIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public LinkPreviewReadyHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task HandleAsync(LinkPreviewReadyIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt.RoomId == Guid.Empty) return; // safety guard

        await _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "LinkPreviewUpdated",
            new
            {
                messageId    = evt.MessageId,
                url          = evt.Url,
                title        = evt.Title,
                description  = evt.Description,
                thumbnailUrl = evt.ThumbnailUrl,
                isDismissed  = evt.IsDismissed,
            },
            cancellationToken);
    }
}
