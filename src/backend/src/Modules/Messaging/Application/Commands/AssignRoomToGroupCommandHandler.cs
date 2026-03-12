using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed class AssignRoomToGroupCommandHandler : IRequestHandler<AssignRoomToGroupCommand>
{
    private readonly ISidebarGroupRepository _groups;

    public AssignRoomToGroupCommandHandler(ISidebarGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task Handle(AssignRoomToGroupCommand request, CancellationToken cancellationToken)
    {
        await _groups.AssignRoomAsync(request.GroupId, request.UserId, request.RoomId, cancellationToken);
    }
}

public sealed class UnassignRoomFromGroupCommandHandler : IRequestHandler<UnassignRoomFromGroupCommand>
{
    private readonly ISidebarGroupRepository _groups;

    public UnassignRoomFromGroupCommandHandler(ISidebarGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task Handle(UnassignRoomFromGroupCommand request, CancellationToken cancellationToken)
    {
        await _groups.UnassignRoomAsync(request.UserId, request.RoomId, cancellationToken);
    }
}
