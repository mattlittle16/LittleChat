using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record AdminRemoveAvatarCommand(Guid UserId, Guid AdminId, string AdminName)
    : IRequest<AdminRemoveAvatarResult>;

public abstract record AdminRemoveAvatarResult
{
    public sealed record Success(Guid UserId, string DisplayName) : AdminRemoveAvatarResult;
    public sealed record UserNotFound() : AdminRemoveAvatarResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<UserNotFound, TResult> onUserNotFound) =>
        this switch
        {
            Success s    => onSuccess(s),
            UserNotFound => onUserNotFound((UserNotFound)this),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
