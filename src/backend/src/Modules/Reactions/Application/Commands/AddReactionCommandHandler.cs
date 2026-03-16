using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Reactions.Application.Commands;

public sealed class AddReactionCommandHandler : IRequestHandler<AddReactionCommand, (bool Added, int Count)>
{
    private readonly IReactionRepository _repo;
    private readonly IEventBus _eventBus;

    public AddReactionCommandHandler(IReactionRepository repo, IEventBus eventBus)
    {
        _repo = repo;
        _eventBus = eventBus;
    }

    public async Task<(bool Added, int Count)> Handle(
        AddReactionCommand request,
        CancellationToken cancellationToken)
    {
        var (added, count, users) = await _repo.ToggleAsync(
            request.MessageId,
            request.UserId,
            request.Emoji,
            cancellationToken);

        var (authorUserId, messageContent, roomName) = await _repo.GetMessageInfoAsync(
            request.MessageId, cancellationToken);

        var contentPreview = authorUserId != Guid.Empty
            ? $"{request.Emoji} \u2014 {messageContent[..Math.Min(messageContent.Length, 80)]}"
            : string.Empty;

        await _eventBus.PublishAsync(new ReactionUpdatedIntegrationEvent
        {
            MessageId          = request.MessageId,
            RoomId             = request.RoomId,
            Emoji              = request.Emoji,
            Count              = count,
            Added              = added,
            Users              = users,
            ReactorUserId      = request.UserId,
            ReactorDisplayName = request.DisplayName,
            AuthorUserId       = authorUserId,
            RoomName           = roomName,
            ContentPreview     = contentPreview,
        }, cancellationToken);

        return (added, count);
    }
}
