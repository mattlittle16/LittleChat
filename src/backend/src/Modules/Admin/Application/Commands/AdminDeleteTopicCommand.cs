using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record AdminDeleteTopicCommand(Guid TopicId, Guid AdminId, string AdminName)
    : IRequest<AdminDeleteTopicResult>;

public abstract record AdminDeleteTopicResult
{
    public sealed record Success(Guid TopicId, string Name) : AdminDeleteTopicResult;
    public sealed record TopicNotFound() : AdminDeleteTopicResult;
    public sealed record IsProtected() : AdminDeleteTopicResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<TopicNotFound, TResult> onTopicNotFound,
        Func<IsProtected, TResult> onIsProtected) =>
        this switch
        {
            Success s   => onSuccess(s),
            TopicNotFound => onTopicNotFound((TopicNotFound)this),
            IsProtected   => onIsProtected((IsProtected)this),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
