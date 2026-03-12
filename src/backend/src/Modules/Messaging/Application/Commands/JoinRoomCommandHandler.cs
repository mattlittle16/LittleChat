using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class JoinRoomCommandHandler : IRequestHandler<JoinRoomCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IEventBus _eventBus;

    public JoinRoomCommandHandler(IRoomRepository rooms, IEventBus eventBus)
    {
        _rooms = rooms;
        _eventBus = eventBus;
    }

    public async Task Handle(JoinRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken)
            ?? throw new KeyNotFoundException("Topic not found.");

        if (room.IsPrivate)
            throw new UnauthorizedAccessException("Cannot join a private topic.");

        var alreadyMember = await _rooms.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (alreadyMember)
            throw new InvalidOperationException("Already a member of this topic.");

        await _rooms.AddMemberAsync(request.RoomId, request.UserId, cancellationToken);

        await _eventBus.PublishAsync(new MemberAddedIntegrationEvent
        {
            RoomId      = request.RoomId,
            AddedUserId = request.UserId,
            RoomName    = room.Name,
        }, cancellationToken);
    }
}
