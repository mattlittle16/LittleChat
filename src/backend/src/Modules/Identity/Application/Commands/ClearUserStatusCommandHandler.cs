using Identity.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Identity.Application.Commands;

public sealed record ClearUserStatusCommand(Guid UserId) : IRequest;

public sealed class ClearUserStatusCommandHandler : IRequestHandler<ClearUserStatusCommand>
{
    private readonly IUserRepository _users;
    private readonly IEventBus _eventBus;

    public ClearUserStatusCommandHandler(IUserRepository users, IEventBus eventBus)
    {
        _users = users;
        _eventBus = eventBus;
    }

    public async Task Handle(ClearUserStatusCommand request, CancellationToken cancellationToken)
    {
        await _users.UpdateStatusAsync(request.UserId, null, null, null, cancellationToken);

        await _eventBus.PublishAsync(new UserStatusUpdatedIntegrationEvent
        {
            UserId = request.UserId,
            Emoji  = null,
            Text   = null,
            Color  = null,
        }, cancellationToken);
    }
}
