using MediatR;

namespace Identity.Application.Commands;

public sealed record SetUserStatusCommand(
    Guid UserId,
    string? Emoji,
    string? Text,
    string? Color
) : IRequest<SetUserStatusResult>;

public sealed record SetUserStatusResult(string? Emoji, string? Text, string? Color);
