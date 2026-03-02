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
    private readonly IFileStorageService? _fileStorage;

    public SendMessageCommandHandler(
        IMessageRepository messages,
        IEventBus eventBus,
        IFileStorageService? fileStorage = null)
    {
        _messages = messages;
        _eventBus = eventBus;
        _fileStorage = fileStorage;
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

        // Save file before creating message (rollback on DB failure)
        string? savedPath = null;
        if (request.FileStream is not null && request.OriginalFileName is not null && _fileStorage is not null)
        {
            savedPath = await _fileStorage.SaveAsync(request.FileStream, request.OriginalFileName, cancellationToken);
        }

        var message = new Message(
            Id: request.MessageId,
            RoomId: request.RoomId,
            UserId: request.UserId,
            AuthorDisplayName: request.AuthorDisplayName,
            AuthorAvatarUrl: request.AuthorAvatarUrl,
            Content: request.Content,
            FilePath: savedPath,
            FileName: savedPath is not null ? request.OriginalFileName : null,
            FileSize: savedPath is not null ? request.FileSize : null,
            CreatedAt: DateTime.UtcNow,
            EditedAt: null,
            ExpiresAt: DateTime.UtcNow.AddDays(30)
        );

        try
        {
            // Idempotent — duplicate MessageId returns silently
            await _messages.CreateAsync(message, cancellationToken);
        }
        catch
        {
            // Rollback file on DB failure
            if (savedPath is not null && _fileStorage is not null)
                await _fileStorage.DeleteAsync(savedPath, cancellationToken);
            throw;
        }

        // Persist-first, then broadcast (Constitution Principle I)
        await _eventBus.PublishAsync(new MessageSentIntegrationEvent
        {
            MessageId = message.Id,
            RoomId = message.RoomId,
            UserId = message.UserId,
            DisplayName = message.AuthorDisplayName,
            AvatarUrl = message.AuthorAvatarUrl,
            Content = message.Content,
            FileName = message.FileName,
            FileSize = message.FileSize,
            CreatedAt = message.CreatedAt,
        }, cancellationToken);

        return message.Id;
    }
}
