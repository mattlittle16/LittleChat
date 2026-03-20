using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record ForceLogoutUserCommand(Guid TargetUserId, Guid AdminId, string AdminName, int BanDurationHours)
    : IRequest<ForceLogoutUserCommandResult>;

public abstract record ForceLogoutUserCommandResult
{
    public sealed record Success(Guid UserId, string DisplayName) : ForceLogoutUserCommandResult;
    public sealed record NotFound() : ForceLogoutUserCommandResult;
    public sealed record SelfLogout() : ForceLogoutUserCommandResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<NotFound, TResult> onNotFound,
        Func<SelfLogout, TResult> onSelfLogout) =>
        this switch
        {
            Success s => onSuccess(s),
            NotFound n => onNotFound(n),
            SelfLogout sl => onSelfLogout(sl),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
