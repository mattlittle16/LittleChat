using System.Text.RegularExpressions;
using MediatR;
using Messaging.Domain;
using Microsoft.Extensions.Logging;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, SendMessageResult>
{
    private const int MaxContentLength = 4_000;
    private static readonly Regex MentionRegex = new(@"@(\w+)", RegexOptions.Compiled);
    private static readonly Regex TopicRegex   = new(@"@topic\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IMessageRepository _messages;
    private readonly IRoomRepository _rooms;
    private readonly IEventBus _eventBus;
    private readonly IFileStorageService? _fileStorage;
    private readonly IUserLookupService? _userLookup;
    private readonly ILogger<SendMessageCommandHandler> _logger;

    public SendMessageCommandHandler(
        IMessageRepository messages,
        IRoomRepository rooms,
        IEventBus eventBus,
        ILogger<SendMessageCommandHandler> logger,
        IFileStorageService? fileStorage = null,
        IUserLookupService? userLookup = null)
    {
        _messages = messages;
        _rooms = rooms;
        _eventBus = eventBus;
        _logger = logger;
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

        // Resolve quoted message if provided
        string? quotedAuthorDn = null;
        string? quotedSnapshot = null;
        Message? quotedMessage = null;
        if (request.QuotedMessageId.HasValue)
        {
            quotedMessage = await _messages.GetByIdAsync(request.QuotedMessageId.Value, cancellationToken);
            if (quotedMessage is null)
                throw new InvalidOperationException("Quoted message not found.");
            quotedAuthorDn = quotedMessage.AuthorDisplayName;
            quotedSnapshot = quotedMessage.Content.Length > 500
                ? quotedMessage.Content[..500]
                : quotedMessage.Content;
        }

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
                        FileSize:     result.ActualFileSize,
                        FilePath:     result.RelativePath,
                        ContentType:  result.ContentType,
                        IsImage:      result.IsImage,
                        DisplayOrder: displayOrder++
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save attachment {FileName} for message {MessageId}", file.FileName, request.MessageId);
                    failedFileNames.Add(file.FileName);
                }
            }
        }

        // If every file upload failed and there's no text, don't persist or broadcast a blank message
        if (!hasContent && savedAttachments.Count == 0)
            return new SendMessageResult(request.MessageId, failedFileNames);

        var message = new Message(
            Id:                      request.MessageId,
            RoomId:                  request.RoomId,
            UserId:                  request.UserId,
            AuthorDisplayName:       request.AuthorDisplayName,
            AuthorAvatarUrl:         request.AuthorAvatarUrl,
            Content:                 request.Content,
            Attachments:             savedAttachments,
            CreatedAt:               DateTime.UtcNow,
            EditedAt:                null,
            ExpiresAt:               DateTime.UtcNow.AddDays(30),
            Reactions:               [],
            MessageType:             "text",
            QuotedMessageId:         request.QuotedMessageId,
            QuotedAuthorDisplayName: quotedAuthorDn,
            QuotedContentSnapshot:   quotedSnapshot
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

        var contentPreview = hasContent && message.Content.Length > 100
            ? message.Content[..100] + "…"
            : message.Content;

        // Persist-first, then broadcast (Constitution Principle I)
        await _eventBus.PublishAsync(new MessageSentIntegrationEvent
        {
            MessageId   = message.Id,
            RoomId      = message.RoomId,
            UserId      = message.UserId!.Value, // non-null for user-sent messages
            DisplayName = message.AuthorDisplayName,
            AvatarUrl   = message.AuthorAvatarUrl,
            Content     = message.Content,
            Attachments = message.Attachments
                .Select(a => new AttachmentEventData(a.Id, a.FileName, a.FileSize, a.ContentType, a.IsImage))
                .ToList(),
            CreatedAt   = message.CreatedAt,
            MessageType = "text",
            QuoteData   = request.QuotedMessageId.HasValue
                ? new QuoteDto(request.QuotedMessageId, quotedAuthorDn!, quotedSnapshot!, true)
                : null,
        }, cancellationToken);

        // Notify quoted message author (no self-quotes)
        if (request.QuotedMessageId.HasValue
            && quotedMessage!.UserId.HasValue
            && quotedMessage.UserId != request.UserId)
        {
            var roomNameForQuote = await _messages.GetRoomNameAsync(message.RoomId, cancellationToken) ?? string.Empty;
            await _eventBus.PublishAsync(new MessageQuotedIntegrationEvent
            {
                MessageId             = message.Id,
                RoomId                = message.RoomId,
                RoomName              = roomNameForQuote,
                QuotedMessageAuthorId = quotedMessage.UserId!.Value,
                QuoterUserId          = message.UserId!.Value,
                QuoterDisplayName     = message.AuthorDisplayName,
                ContentPreview        = contentPreview,
            }, cancellationToken);
        }

        // Detect @mentions and publish one event per unique mentioned user
        if (_userLookup is not null && hasContent)
        {
            var mentions = MentionRegex.Matches(message.Content)
                .Select(m => m.Groups[1].Value)
                .Where(n => !n.Equals("topic", StringComparison.OrdinalIgnoreCase))
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
                    FromUserId      = message.UserId!.Value,
                    FromDisplayName = message.AuthorDisplayName,
                    ContentPreview  = contentPreview,
                }, cancellationToken);
            }
        }

        // Detect @topic / DM notifications — fetch room once for both checks
        if (hasContent)
        {
            var hasTopicMention = TopicRegex.IsMatch(message.Content);
            var room = await _rooms.GetByIdAsync(message.RoomId, cancellationToken);
            if (room is not null)
            {
                var roomName  = await _messages.GetRoomNameAsync(message.RoomId, cancellationToken) ?? string.Empty;
                var memberIds = await _rooms.GetRoomMemberIdsAsync(message.RoomId, cancellationToken);

                if (!room.IsDm && hasTopicMention)
                {
                    // @topic in a topic room — alert all members except sender
                    var recipients = memberIds.Where(id => id != message.UserId).ToList();
                    if (recipients.Count > 0)
                    {
                        await _eventBus.PublishAsync(new TopicAlertIntegrationEvent
                        {
                            MessageId         = message.Id,
                            RoomId            = message.RoomId,
                            RoomName          = roomName,
                            SenderUserId      = message.UserId!.Value,
                            SenderDisplayName = message.AuthorDisplayName,
                            ContentPreview    = contentPreview,
                            RecipientUserIds  = recipients,
                        }, cancellationToken);
                    }
                }
                else if (room.IsDm)
                {
                    // DM message — notify the non-sending participant
                    var recipientId = memberIds.FirstOrDefault(id => id != message.UserId);
                    if (recipientId != default)
                    {
                        await _eventBus.PublishAsync(new DmMessageSentIntegrationEvent
                        {
                            MessageId         = message.Id,
                            RoomId            = message.RoomId,
                            RoomName          = roomName,
                            SenderUserId      = message.UserId!.Value,
                            SenderDisplayName = message.AuthorDisplayName,
                            ContentPreview    = contentPreview,
                            RecipientUserId   = recipientId,
                        }, cancellationToken);
                    }
                }
            }
        }

        return new SendMessageResult(message.Id, failedFileNames);
    }
}
