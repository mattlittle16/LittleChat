namespace Messaging.Application.Commands;

public sealed record SendMessageResult(
    Guid                  MessageId,
    IReadOnlyList<string> FailedFileNames
);
