using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class InviteToRoomCommandHandler : IRequestHandler<InviteToRoomCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public InviteToRoomCommandHandler(IRoomRepository rooms, IMessageRepository messages, IEventBus eventBus)
    {
        _rooms    = rooms;
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task Handle(InviteToRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken)
            ?? throw new KeyNotFoundException("Topic not found.");

        var inviterIsMember = await _rooms.IsMemberAsync(request.RoomId, request.InviterUserId, cancellationToken);
        if (!inviterIsMember)
            throw new UnauthorizedAccessException("You are not a member of this topic.");

        var alreadyMember = await _rooms.IsMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);
        if (alreadyMember)
            throw new InvalidOperationException("User is already a member of this topic.");

        await _rooms.AddMemberAsync(request.RoomId, request.TargetUserId, cancellationToken);

        await _eventBus.PublishAsync(new MemberAddedIntegrationEvent
        {
            RoomId      = request.RoomId,
            AddedUserId = request.TargetUserId,
            RoomName    = room.Name,
        }, cancellationToken);

        // Look up the added user's display name from the now-current member list
        var members = await _rooms.GetMembersAsync(request.RoomId, cancellationToken);
        var targetName = members.FirstOrDefault(m => m.UserId == request.TargetUserId)?.DisplayName ?? "A new member";

        var systemMessage = new Message(
            Id:                Guid.NewGuid(),
            RoomId:            request.RoomId,
            UserId:            null,
            AuthorDisplayName: "System",
            AuthorAvatarUrl:   null,
            Content:           $"{request.InviterDisplayName} added {targetName} to the topic.",
            Attachments:       [],
            CreatedAt:         DateTime.UtcNow,
            EditedAt:          null,
            ExpiresAt:         DateTime.UtcNow.AddDays(30),
            Reactions:         [],
            IsSystem:          true
        );

        await _messages.CreateAsync(systemMessage, cancellationToken);

        await _eventBus.PublishAsync(new MessageSentIntegrationEvent
        {
            MessageId   = systemMessage.Id,
            RoomId      = systemMessage.RoomId,
            UserId      = Guid.Empty,
            DisplayName = "System",
            AvatarUrl   = null,
            Content     = systemMessage.Content,
            Attachments = [],
            CreatedAt   = systemMessage.CreatedAt,
            IsSystem    = true,
        }, cancellationToken);
    }
}
