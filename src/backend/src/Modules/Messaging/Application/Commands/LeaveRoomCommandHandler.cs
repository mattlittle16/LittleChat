using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class LeaveRoomCommandHandler : IRequestHandler<LeaveRoomCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly ISender _sender;
    private readonly IEventBus _eventBus;

    public LeaveRoomCommandHandler(IRoomRepository rooms, ISender sender, IEventBus eventBus)
    {
        _rooms   = rooms;
        _sender  = sender;
        _eventBus = eventBus;
    }

    public async Task Handle(LeaveRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken)
            ?? throw new KeyNotFoundException("Topic not found.");

        var isMember = await _rooms.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("You are not a member of this topic.");

        if (room.IsProtected)
            throw new InvalidOperationException("You cannot leave the General topic.");

        var isOwner = await _rooms.IsOwnerAsync(request.RoomId, request.UserId, cancellationToken);
        if (isOwner)
        {
            var memberCount = await _rooms.GetMemberCountAsync(request.RoomId, cancellationToken);
            if (memberCount > 1)
            {
                // Must transfer ownership before leaving when others are present
                if (request.NewOwnerUserId is null || string.IsNullOrEmpty(request.NewOwnerDisplayName))
                    throw new InvalidOperationException(
                        "You must transfer ownership to another member before leaving.");

                await _sender.Send(new TransferOwnershipCommand(
                    request.RoomId,
                    request.UserId,
                    request.UserDisplayName,
                    request.NewOwnerUserId.Value,
                    request.NewOwnerDisplayName!), cancellationToken);
            }
        }

        await _rooms.RemoveMemberAsync(request.RoomId, request.UserId, cancellationToken);

        await _eventBus.PublishAsync(new MemberRemovedIntegrationEvent
        {
            RoomId        = request.RoomId,
            RemovedUserId = request.UserId,
        }, cancellationToken);
    }
}
