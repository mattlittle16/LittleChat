using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IEventBus _eventBus;

    public RemoveMemberCommandHandler(IRoomRepository rooms, IEventBus eventBus)
    {
        _rooms = rooms;
        _eventBus = eventBus;
    }

    public async Task Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var isOwner = await _rooms.IsOwnerAsync(request.RoomId, request.OwnerUserId, cancellationToken);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the topic owner can remove members.");

        if (request.TargetUserId == request.OwnerUserId)
            throw new InvalidOperationException("The owner cannot remove themselves; use the leave endpoint and transfer ownership first.");

        var isMember = await _rooms.IsMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);
        if (!isMember)
            throw new InvalidOperationException("User is not a member of this topic.");

        await _rooms.RemoveMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);

        await _eventBus.PublishAsync(new MemberRemovedIntegrationEvent
        {
            RoomId         = request.RoomId,
            RemovedUserId  = request.TargetUserId,
        }, cancellationToken);
    }
}
