using MediatR;

namespace Messaging.Application.Commands;

public sealed record DeleteRoomCommand(Guid RoomId, Guid RequestingUserId) : IRequest;
