using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class TransferOwnershipCommandHandler : IRequestHandler<TransferOwnershipCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public TransferOwnershipCommandHandler(IRoomRepository rooms, IMessageRepository messages, IEventBus eventBus)
    {
        _rooms    = rooms;
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task Handle(TransferOwnershipCommand request, CancellationToken cancellationToken)
    {
        var isOwner = await _rooms.IsOwnerAsync(request.RoomId, request.CurrentOwnerUserId, cancellationToken);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the current topic owner can transfer ownership.");

        var newOwnerIsMember = await _rooms.IsMemberAsync(request.RoomId, request.NewOwnerUserId, cancellationToken);
        if (!newOwnerIsMember)
            throw new InvalidOperationException("The new owner must be a current member of the topic.");

        if (request.NewOwnerUserId == request.CurrentOwnerUserId)
            throw new InvalidOperationException("Cannot transfer ownership to yourself.");

        await _rooms.SetOwnerAsync(request.RoomId, request.NewOwnerUserId, cancellationToken);

        // Persist system message announcing the transfer (IsSystem = true, UserId = null)
        var systemMessage = new Message(
            Id:                Guid.NewGuid(),
            RoomId:            request.RoomId,
            UserId:            null,
            AuthorDisplayName: "System",
            AuthorAvatarUrl:   null,
            Content:           $"{request.CurrentOwnerDisplayName} transferred ownership to {request.NewOwnerDisplayName}.",
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
            UserId      = Guid.Empty, // system sender
            DisplayName = "System",
            AvatarUrl   = null,
            Content     = systemMessage.Content,
            Attachments = [],
            CreatedAt   = systemMessage.CreatedAt,
            IsSystem    = true,
        }, cancellationToken);
    }
}
