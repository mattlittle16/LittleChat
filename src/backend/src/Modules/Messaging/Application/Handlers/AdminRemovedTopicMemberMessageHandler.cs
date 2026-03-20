using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Handlers;

public sealed class AdminRemovedTopicMemberMessageHandler : IIntegrationEventHandler<AdminRemovedTopicMemberIntegrationEvent>
{
    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public AdminRemovedTopicMemberMessageHandler(IMessageRepository messages, IEventBus eventBus)
    {
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(AdminRemovedTopicMemberIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        var content = $"{evt.DisplayName} has been removed from this topic.";

        var systemMessage = new Message(
            Id:                Guid.NewGuid(),
            RoomId:            evt.RoomId,
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
