using MediatR;

namespace Messaging.Application.Commands;

public sealed record LeaveRoomCommand(
    Guid RoomId,
    Guid UserId,
    string UserDisplayName,
    Guid? NewOwnerUserId = null,
    string? NewOwnerDisplayName = null
) : IRequest;
