using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record AdminUpdateAvatarCommand(Guid UserId, Stream FileStream, string FileName, Guid AdminId, string AdminName)
    : IRequest<AdminUpdateAvatarResult>;

public abstract record AdminUpdateAvatarResult
{
    public sealed record Success(Guid UserId, string DisplayName, string ProfileImageUrl) : AdminUpdateAvatarResult;
    public sealed record UserNotFound() : AdminUpdateAvatarResult;
    public sealed record InvalidFile() : AdminUpdateAvatarResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<UserNotFound, TResult> onUserNotFound,
        Func<InvalidFile, TResult> onInvalidFile) =>
        this switch
        {
            Success s   => onSuccess(s),
            UserNotFound => onUserNotFound((UserNotFound)this),
            InvalidFile  => onInvalidFile((InvalidFile)this),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
