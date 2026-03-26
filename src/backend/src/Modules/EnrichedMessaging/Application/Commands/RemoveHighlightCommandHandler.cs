using EnrichedMessaging.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Application.Commands;

public sealed record RemoveHighlightCommand(Guid RoomId, Guid HighlightId, Guid UserId) : IRequest;

public sealed class RemoveHighlightCommandHandler : IRequestHandler<RemoveHighlightCommand>
{
    private readonly IHighlightRepository _highlights;
    private readonly IEventBus _eventBus;

    public RemoveHighlightCommandHandler(IHighlightRepository highlights, IEventBus eventBus)
    {
        _highlights = highlights;
        _eventBus = eventBus;
    }

    public async Task Handle(RemoveHighlightCommand request, CancellationToken cancellationToken)
    {
        var isMember = await _highlights.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        var deleted = await _highlights.DeleteAsync(request.HighlightId, cancellationToken);
        if (!deleted)
            throw new InvalidOperationException("Highlight not found.");

        await _eventBus.PublishAsync(new HighlightChangedIntegrationEvent
        {
            Action = "removed",
            HighlightId = request.HighlightId,
            RoomId = request.RoomId,
            MessageId = Guid.Empty,
            HighlightedByDisplayName = string.Empty,
            HighlightedAt = DateTime.UtcNow,
        }, cancellationToken);
    }
}
