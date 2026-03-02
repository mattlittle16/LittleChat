using MediatR;

namespace Messaging.Application.Commands;

public sealed record DeleteMessageCommand(
    Guid MessageId,
    Guid RoomId,
    Guid UserId)
    : IRequest;
