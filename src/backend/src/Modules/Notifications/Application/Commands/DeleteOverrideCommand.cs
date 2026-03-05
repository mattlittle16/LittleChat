using MediatR;
using Notifications.Domain;

namespace Notifications.Application.Commands;

public sealed record DeleteOverrideCommand(Guid UserId, Guid RoomId) : IRequest;

public sealed class DeleteOverrideCommandHandler : IRequestHandler<DeleteOverrideCommand>
{
    private readonly IConversationOverrideRepository _repo;

    public DeleteOverrideCommandHandler(IConversationOverrideRepository repo)
    {
        _repo = repo;
    }

    public async Task Handle(DeleteOverrideCommand request, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(request.UserId, request.RoomId, cancellationToken);
    }
}
