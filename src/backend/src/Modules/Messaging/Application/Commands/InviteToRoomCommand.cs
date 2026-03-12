using MediatR;

namespace Messaging.Application.Commands;

public sealed record InviteToRoomCommand(Guid RoomId, Guid InviterUserId, Guid TargetUserId) : IRequest;
