using MediatR;

namespace Messaging.Application.Commands;

public sealed record SendMessageCommand(
    Guid MessageId,   // client-provided UUID for idempotency
    Guid RoomId,
    Guid UserId,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Content
) : IRequest<Guid>;
