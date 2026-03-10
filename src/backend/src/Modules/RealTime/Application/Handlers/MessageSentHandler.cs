using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class MessageSentHandler : IIntegrationEventHandler<MessageSentIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public MessageSentHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task HandleAsync(MessageSentIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        var dto = new MessageDto(
            Id:          evt.MessageId,
            RoomId:      evt.RoomId,
            Author:      new AuthorDto(evt.UserId, evt.DisplayName, evt.AvatarUrl),
            Content:     evt.Content,
            Attachments: evt.Attachments
                .Select(a => new AttachmentDto(
                    a.AttachmentId,
                    a.FileName,
                    a.FileSize,
                    a.ContentType,
                    a.IsImage,
                    $"/api/files/attachments/{a.AttachmentId}"))
                .ToList(),
            Reactions:   [],
            CreatedAt:   evt.CreatedAt,
            EditedAt:    null
        );

        await _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "ReceiveMessage",
            dto,
            cancellationToken);
    }
}
