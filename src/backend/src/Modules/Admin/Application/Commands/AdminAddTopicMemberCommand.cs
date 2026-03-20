using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record AdminAddTopicMemberCommand(Guid TopicId, Guid UserId, Guid AdminId, string AdminName)
    : IRequest<AdminAddTopicMemberResult>;

public abstract record AdminAddTopicMemberResult
{
    public sealed record Success(Guid UserId, string DisplayName) : AdminAddTopicMemberResult;
    public sealed record TopicNotFound() : AdminAddTopicMemberResult;
    public sealed record UserNotFound() : AdminAddTopicMemberResult;
    public sealed record AlreadyMember() : AdminAddTopicMemberResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<TopicNotFound, TResult> onTopicNotFound,
        Func<UserNotFound, TResult> onUserNotFound,
        Func<AlreadyMember, TResult> onAlreadyMember) =>
        this switch
        {
            Success s      => onSuccess(s),
            TopicNotFound  => onTopicNotFound((TopicNotFound)this),
            UserNotFound   => onUserNotFound((UserNotFound)this),
            AlreadyMember  => onAlreadyMember((AlreadyMember)this),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
