using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record AdminUpdateDisplayNameCommand(Guid UserId, string DisplayName, Guid AdminId, string AdminName)
    : IRequest<AdminUpdateDisplayNameResult>;

public abstract record AdminUpdateDisplayNameResult
{
    public sealed record Success(Guid UserId, string DisplayName) : AdminUpdateDisplayNameResult;
    public sealed record UserNotFound() : AdminUpdateDisplayNameResult;
    public sealed record InvalidName() : AdminUpdateDisplayNameResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<UserNotFound, TResult> onUserNotFound,
        Func<InvalidName, TResult> onInvalidName) =>
        this switch
        {
            Success s    => onSuccess(s),
            UserNotFound => onUserNotFound((UserNotFound)this),
            InvalidName  => onInvalidName((InvalidName)this),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
