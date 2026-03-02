using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Queries;

public sealed record GetMessagesQuery(
    Guid RoomId,
    Guid UserId,
    DateTime? Before,
    Guid? BeforeId,
    int Limit = 50
) : IRequest<MessagePage>;
