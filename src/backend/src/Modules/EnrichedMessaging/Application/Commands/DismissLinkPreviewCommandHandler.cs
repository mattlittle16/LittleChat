using EnrichedMessaging.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Application.Commands;

public sealed record DismissLinkPreviewCommand(Guid MessageId, Guid UserId) : IRequest;

public sealed class DismissLinkPreviewCommandHandler : IRequestHandler<DismissLinkPreviewCommand>
{
    private readonly ILinkPreviewRepository _previews;
    private readonly IEventBus _eventBus;

    public DismissLinkPreviewCommandHandler(ILinkPreviewRepository previews, IEventBus eventBus)
    {
        _previews = previews;
        _eventBus = eventBus;
    }

    public async Task Handle(DismissLinkPreviewCommand request, CancellationToken cancellationToken)
    {
        var preview = await _previews.GetByMessageIdAsync(request.MessageId, cancellationToken);
        if (preview is null)
            throw new InvalidOperationException("No link preview found for this message.");

        var roomId = await _previews.GetRoomIdByMessageAndUserAsync(request.MessageId, request.UserId, cancellationToken);
        if (roomId is null)
            throw new UnauthorizedAccessException("Only the message sender may dismiss a link preview.");

        await _previews.DismissAsync(request.MessageId, request.UserId, cancellationToken);

        await _eventBus.PublishAsync(new LinkPreviewReadyIntegrationEvent
        {
            MessageId    = request.MessageId,
            RoomId       = roomId.Value,
            Url          = preview.Url,
            Title        = preview.Title,
            Description  = preview.Description,
            ThumbnailUrl = preview.ThumbnailUrl,
            IsDismissed  = true,
        }, cancellationToken);
    }
}
