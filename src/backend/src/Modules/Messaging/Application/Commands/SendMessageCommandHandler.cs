using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, Guid>
{
    private const int MaxContentLength = 4_000;

    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public SendMessageCommandHandler(IMessageRepository messages, IEventBus eventBus)
    {
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Message content cannot be empty.");

        if (request.Content.Length > MaxContentLength)
            throw new InvalidOperationException($"Message content exceeds {MaxContentLength} characters.");

        var isMember = await _messages.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        var message = new Message(
            Id: request.MessageId,
            RoomId: request.RoomId,
            UserId: request.UserId,
            AuthorDisplayName: request.AuthorDisplayName,
            AuthorAvatarUrl: request.AuthorAvatarUrl,
            Content: request.Content,
            FilePath: null,
            FileName: null,
            FileSize: null,
            CreatedAt: DateTime.UtcNow,
            EditedAt: null,
            ExpiresAt: DateTime.UtcNow.AddDays(30)
        );

        // Idempotent — duplicate MessageId returns silently
        await _messages.CreateAsync(message, cancellationToken);

        // Persist-first, then broadcast (Constitution Principle I)
        await _eventBus.PublishAsync(new MessageSentIntegrationEvent
        {
            MessageId = message.Id,
            RoomId = message.RoomId,
            UserId = message.UserId,
            DisplayName = message.AuthorDisplayName,
            AvatarUrl = message.AuthorAvatarUrl,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
        }, cancellationToken);

        return message.Id;
    }
}
