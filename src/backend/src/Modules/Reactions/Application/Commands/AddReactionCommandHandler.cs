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

        await _eventBus.PublishAsync(new ReactionUpdatedIntegrationEvent
        {
            MessageId = request.MessageId,
            RoomId = request.RoomId,
            Emoji = request.Emoji,
            Count = count,
            Added = added,
            Users = users,
        }, cancellationToken);

        return (added, count);
    }
}
