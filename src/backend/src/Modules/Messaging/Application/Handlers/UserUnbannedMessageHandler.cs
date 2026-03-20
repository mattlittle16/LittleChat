using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Handlers;

public sealed class UserUnbannedMessageHandler : IIntegrationEventHandler<UserUnbannedIntegrationEvent>
{
    private readonly IRoomRepository _rooms;
    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public UserUnbannedMessageHandler(IRoomRepository rooms, IMessageRepository messages, IEventBus eventBus)
    {
        _rooms = rooms;
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(UserUnbannedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        var topicIds = await _rooms.GetTopicIdsForUserAsync(evt.TargetUserId, cancellationToken);
        if (topicIds.Count == 0) return;

        var content = $"{evt.TargetDisplayName} has been unbanned.";

        foreach (var topicId in topicIds)
        {
            var systemMessage = new Message(
                Id:                Guid.NewGuid(),
                RoomId:            topicId,
                UserId:            null,
                AuthorDisplayName: "Chat News Bot",
                AuthorAvatarUrl:   null,
                Content:           content,
                Attachments:       [],
                CreatedAt:         DateTime.UtcNow,
                EditedAt:          null,
                ExpiresAt:         DateTime.UtcNow.AddDays(30),
                Reactions:         [],
                IsSystem:          false
            );

            await _messages.CreateAsync(systemMessage, cancellationToken);

            await _eventBus.PublishAsync(new MessageSentIntegrationEvent
            {
                MessageId   = systemMessage.Id,
                RoomId      = systemMessage.RoomId,
                UserId      = Guid.Empty,
                DisplayName = "Chat News Bot",
                AvatarUrl   = null,
                Content     = systemMessage.Content,
                Attachments = [],
                CreatedAt   = systemMessage.CreatedAt,
                IsSystem    = false,
            }, cancellationToken);
        }
    }
}
