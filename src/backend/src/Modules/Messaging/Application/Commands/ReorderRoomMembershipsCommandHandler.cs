using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed class ReorderRoomMembershipsCommandHandler : IRequestHandler<ReorderRoomMembershipsCommand>
{
    private readonly ISidebarGroupRepository _groups;
    private readonly IRoomRepository _rooms;

    public ReorderRoomMembershipsCommandHandler(ISidebarGroupRepository groups, IRoomRepository rooms)
    {
        _groups = groups;
        _rooms = rooms;
    }

    public async Task Handle(ReorderRoomMembershipsCommand request, CancellationToken cancellationToken)
    {
        foreach (var roomId in request.RoomIds)
        {
            var isMember = await _rooms.IsMemberAsync(roomId, request.UserId, cancellationToken);
            if (!isMember)
                throw new UnauthorizedAccessException($"User is not a member of room {roomId}.");
        }

        await _groups.ReorderAsync(request.UserId, request.GroupId, request.RoomIds, cancellationToken);
    }
}
