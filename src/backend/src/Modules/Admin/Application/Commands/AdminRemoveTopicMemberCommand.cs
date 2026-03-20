using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record AdminRemoveTopicMemberCommand(Guid TopicId, Guid UserId, Guid AdminId, string AdminName)
    : IRequest<AdminRemoveTopicMemberResult>;

public abstract record AdminRemoveTopicMemberResult
{
    public sealed record Success(Guid UserId, string DisplayName) : AdminRemoveTopicMemberResult;
    public sealed record TopicNotFound() : AdminRemoveTopicMemberResult;
    public sealed record UserNotFound() : AdminRemoveTopicMemberResult;
    public sealed record NotMember() : AdminRemoveTopicMemberResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<TopicNotFound, TResult> onTopicNotFound,
        Func<UserNotFound, TResult> onUserNotFound,
        Func<NotMember, TResult> onNotMember) =>
        this switch
        {
            Success s     => onSuccess(s),
            TopicNotFound => onTopicNotFound((TopicNotFound)this),
            UserNotFound  => onUserNotFound((UserNotFound)this),
            NotMember     => onNotMember((NotMember)this),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
