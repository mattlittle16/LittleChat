using MediatR;

namespace Messaging.Application.Commands;

public sealed record SendMessageCommand(
    Guid MessageId,   // client-provided UUID for idempotency
    Guid RoomId,
    Guid UserId,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Content,                        // may be empty string for file-only messages
    IReadOnlyList<FileUpload> Files,       // empty list for text-only messages
    Guid? QuotedMessageId = null
) : IRequest<SendMessageResult>;
