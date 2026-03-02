using MediatR;

namespace Messaging.Application.Commands;

public sealed record EditMessageCommand(
    Guid MessageId,
    Guid RoomId,
    Guid UserId,
    string Content)
    : IRequest;
