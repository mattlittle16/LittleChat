using System.Text.RegularExpressions;
using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, SendMessageResult>
{
    private const int MaxContentLength = 4_000;
    private static readonly Regex MentionRegex = new(@"@(\w+)", RegexOptions.Compiled);

    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;
    private readonly IFileStorageService? _fileStorage;
    private readonly IUserLookupService? _userLookup;

    public SendMessageCommandHandler(
        IMessageRepository messages,
        IEventBus eventBus,
        IFileStorageService? fileStorage = null,
        IUserLookupService? userLookup = null)
    {
        _messages = messages;
        _eventBus = eventBus;
        _fileStorage = fileStorage;
        _userLookup = userLookup;
    }

    public async Task<SendMessageResult> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasFiles   = request.Files.Count > 0;

        if (!hasContent && !hasFiles)
            throw new InvalidOperationException("Message must have text content or at least one file attachment.");

        if (hasContent && request.Content.Length > MaxContentLength)
            throw new InvalidOperationException($"Message content exceeds {MaxContentLength} characters.");

        var isMember = await _messages.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        // Upload files, collecting results
        var savedAttachments = new List<MessageAttachment>();
        var failedFileNames  = new List<string>();
        var displayOrder     = 0;

        if (_fileStorage is not null)
        {
            foreach (var file in request.Files)
            {
                try
                {
                    var result = await _fileStorage.SaveAsync(file.Stream, file.FileName, cancellationToken);
                    savedAttachments.Add(new MessageAttachment(
                        Id:           Guid.NewGuid(),
                        MessageId:    request.MessageId,
                        FileName:     result.StoredFileName,
                        FileSize:     file.FileSize,
                        FilePath:     result.RelativePath,
                        ContentType:  result.ContentType,
                        IsImage:      result.IsImage,
                        DisplayOrder: displayOrder++
                    ));
                }
                catch
                {
                    failedFileNames.Add(file.FileName);
                }
            }
        }

        var message = new Message(
            Id:                request.MessageId,
            RoomId:            request.RoomId,
            UserId:            request.UserId,
            AuthorDisplayName: request.AuthorDisplayName,
            AuthorAvatarUrl:   request.AuthorAvatarUrl,
            Content:           request.Content,
            Attachments:       savedAttachments,
            CreatedAt:         DateTime.UtcNow,
            EditedAt:          null,
            ExpiresAt:         DateTime.UtcNow.AddDays(30),
            Reactions:         []
        );

        try
        {
            // Idempotent — duplicate MessageId returns silently
            await _messages.CreateAsync(message, cancellationToken);
        }
        catch
        {
            // Rollback all saved files on DB failure
            if (_fileStorage is not null)
            {
                foreach (var att in savedAttachments)
                    await _fileStorage.DeleteAsync(att.FilePath, cancellationToken);
            }
            throw;
        }

        // Persist-first, then broadcast (Constitution Principle I)
        await _eventBus.PublishAsync(new MessageSentIntegrationEvent
        {
            MessageId   = message.Id,
            RoomId      = message.RoomId,
            UserId      = message.UserId,
            DisplayName = message.AuthorDisplayName,
            AvatarUrl   = message.AuthorAvatarUrl,
            Content     = message.Content,
            Attachments = message.Attachments
                .Select(a => new AttachmentEventData(a.Id, a.FileName, a.FileSize, a.ContentType, a.IsImage))
                .ToList(),
            CreatedAt   = message.CreatedAt,
        }, cancellationToken);

        // Detect @mentions and publish one event per unique mentioned user
        if (_userLookup is not null && hasContent)
        {
            var mentions = MentionRegex.Matches(message.Content)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            string? roomName = null;
            foreach (var displayName in mentions)
            {
                var mentionedId = await _userLookup.FindIdByDisplayNameAsync(displayName, cancellationToken);
                if (mentionedId is null || mentionedId == message.UserId) continue;

                roomName ??= await _messages.GetRoomNameAsync(message.RoomId, cancellationToken) ?? string.Empty;

                await _eventBus.PublishAsync(new MentionDetectedIntegrationEvent
                {
                    MessageId       = message.Id,
                    RoomId          = message.RoomId,
                    RoomName        = roomName,
                    MentionedUserId = mentionedId.Value,
                    FromUserId      = message.UserId,
                    FromDisplayName = message.AuthorDisplayName,
                    ContentPreview  = message.Content.Length > 100
                        ? message.Content[..100] + "…"
                        : message.Content,
                }, cancellationToken);
            }
        }

        return new SendMessageResult(message.Id, failedFileNames);
    }
}
