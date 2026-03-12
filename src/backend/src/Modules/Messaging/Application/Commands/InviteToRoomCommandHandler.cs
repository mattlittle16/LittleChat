using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class InviteToRoomCommandHandler : IRequestHandler<InviteToRoomCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IEventBus _eventBus;

    public InviteToRoomCommandHandler(IRoomRepository rooms, IEventBus eventBus)
    {
        _rooms = rooms;
        _eventBus = eventBus;
    }

    public async Task Handle(InviteToRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken)
            ?? throw new KeyNotFoundException("Topic not found.");

        var inviterIsMember = await _rooms.IsMemberAsync(request.RoomId, request.InviterUserId, cancellationToken);
        if (!inviterIsMember)
            throw new UnauthorizedAccessException("You are not a member of this topic.");

        var alreadyMember = await _rooms.IsMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);
        if (alreadyMember)
            throw new InvalidOperationException("User is already a member of this topic.");

        await _rooms.AddMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);

        await _eventBus.PublishAsync(new MemberAddedIntegrationEvent
        {
            RoomId      = request.RoomId,
            AddedUserId = request.TargetUserId,
            RoomName    = room.Name,
        }, cancellationToken);
    }
}
