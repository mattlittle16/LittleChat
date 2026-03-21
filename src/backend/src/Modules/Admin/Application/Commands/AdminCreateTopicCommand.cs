using MediatR;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed record AdminCreateTopicCommand(string Name, Guid AdminId, string AdminName)
    : IRequest<AdminCreateTopicResult>;

public abstract record AdminCreateTopicResult
{
    public sealed record Success(Guid TopicId, string Name) : AdminCreateTopicResult;
    public sealed record InvalidName() : AdminCreateTopicResult;

    public TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<InvalidName, TResult> onInvalidName) =>
        this switch
        {
            Success s   => onSuccess(s),
            InvalidName => onInvalidName((InvalidName)this),
            _ => throw new InvalidOperationException($"Unhandled result type: {GetType().Name}")
        };
}
