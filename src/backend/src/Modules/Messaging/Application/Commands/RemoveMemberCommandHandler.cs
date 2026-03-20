using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public RemoveMemberCommandHandler(IRoomRepository rooms, IMessageRepository messages, IEventBus eventBus)
    {
        _rooms    = rooms;
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var isOwner = await _rooms.IsOwnerAsync(request.RoomId, request.OwnerUserId, cancellationToken);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the topic owner can remove members.");

        if (request.TargetUserId == request.OwnerUserId)
            throw new InvalidOperationException("The owner cannot remove themselves; use the leave endpoint and transfer ownership first.");

        var isMember = await _rooms.IsMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);
        if (!isMember)
            throw new InvalidOperationException("User is not a member of this topic.");

        // Look up target's display name before removing them
        var members = await _rooms.GetMembersAsync(request.RoomId, cancellationToken);
        var targetName = members.FirstOrDefault(m => m.UserId == request.TargetUserId)?.DisplayName ?? "A member";

        await _rooms.RemoveMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);

        await _eventBus.PublishAsync(new MemberRemovedIntegrationEvent
        {
            RoomId        = request.RoomId,
            RemovedUserId = request.TargetUserId,
        }, cancellationToken);

        var systemMessage = new Message(
            Id:                Guid.NewGuid(),
            RoomId:            request.RoomId,
            UserId:            null,
            AuthorDisplayName: "Chat News Bot",
            AuthorAvatarUrl:   null,
            Content:           $"{request.OwnerDisplayName} removed {targetName} from the topic.",
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
