using MediatR;

namespace Messaging.Application.Commands;

public sealed record AssignRoomToGroupCommand(Guid GroupId, Guid UserId, Guid RoomId) : IRequest;
public sealed record UnassignRoomFromGroupCommand(Guid UserId, Guid RoomId) : IRequest;
