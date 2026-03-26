using EnrichedMessaging.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Application.Commands;

public sealed record AddHighlightCommand(
    Guid RoomId, Guid MessageId, Guid UserId, string DisplayName
) : IRequest<AddHighlightResult>;

public sealed record AddHighlightResult(
    Guid Id, Guid MessageId, Guid RoomId, string HighlightedByDisplayName, DateTime HighlightedAt,
    bool IsDeleted, string? ContentPreview, string? AuthorDisplayName, DateTime? MessageCreatedAt
);

public sealed class AddHighlightCommandHandler : IRequestHandler<AddHighlightCommand, AddHighlightResult>
{
    private readonly IHighlightRepository _highlights;
    private readonly IEventBus _eventBus;

    public AddHighlightCommandHandler(IHighlightRepository highlights, IEventBus eventBus)
    {
        _highlights = highlights;
        _eventBus = eventBus;
    }

    public async Task<AddHighlightResult> Handle(AddHighlightCommand request, CancellationToken cancellationToken)
    {
        var isMember = await _highlights.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        var msgExists = await _highlights.MessageExistsInRoomAsync(request.MessageId, request.RoomId, cancellationToken);
        if (!msgExists)
            throw new InvalidOperationException("Message not found in this room.");

        var highlight = await _highlights.AddAsync(request.RoomId, request.MessageId, request.UserId, request.DisplayName, cancellationToken);
        if (highlight is null)
            throw new InvalidOperationException("Failed to create highlight.");

        await _eventBus.PublishAsync(new HighlightChangedIntegrationEvent
        {
            Action                   = "added",
            HighlightId              = highlight.Id,
            RoomId                   = request.RoomId,
            MessageId                = request.MessageId,
            HighlightedByDisplayName = request.DisplayName,
            HighlightedAt            = highlight.HighlightedAt,
            ContentPreview           = highlight.ContentPreview,
            AuthorDisplayName        = highlight.AuthorDisplayName,
        }, cancellationToken);

        return new AddHighlightResult(
            highlight.Id, highlight.MessageId, highlight.RoomId,
            highlight.HighlightedByDisplayName, highlight.HighlightedAt,
            false, highlight.ContentPreview, highlight.AuthorDisplayName, highlight.MessageCreatedAt);
    }
}
