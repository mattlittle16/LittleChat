using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record UnbanUserCommand(Guid TargetUserId, Guid AdminId, string AdminName) : IRequest<UnbanUserCommandResult>;

public abstract record UnbanUserCommandResult
{
    public sealed record Success(Guid UserId, string DisplayName) : UnbanUserCommandResult;
    public sealed record NotFound() : UnbanUserCommandResult;
    public sealed record NotBanned() : UnbanUserCommandResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<NotFound, TResult> onNotFound,
        Func<NotBanned, TResult> onNotBanned) =>
        this switch
        {
            Success s => onSuccess(s),
            NotFound n => onNotFound(n),
            NotBanned nb => onNotBanned(nb),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
