using MediatR;

namespace Messaging.Application.Commands;

public sealed record JoinRoomCommand(Guid RoomId, Guid UserId) : IRequest;
