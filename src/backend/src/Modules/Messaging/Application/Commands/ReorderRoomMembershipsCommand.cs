using MediatR;

namespace Messaging.Application.Commands;

public sealed record ReorderRoomMembershipsCommand(
    Guid UserId,
    Guid? GroupId,
    IReadOnlyList<Guid> RoomIds) : IRequest;
