using MediatR;

namespace Reactions.Application.Commands;

public sealed record AddReactionCommand(
    Guid MessageId,
    Guid RoomId,
    Guid UserId,
    string DisplayName,
    string Emoji)
    : IRequest<(bool Added, int Count)>;
