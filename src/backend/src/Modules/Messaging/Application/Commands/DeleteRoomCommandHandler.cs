using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class DeleteRoomCommandHandler : IRequestHandler<DeleteRoomCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IEventBus _eventBus;

    public DeleteRoomCommandHandler(IRoomRepository rooms, IEventBus eventBus)
    {
        _rooms    = rooms;
        _eventBus = eventBus;
    }

    public async Task Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken)
            ?? throw new KeyNotFoundException($"Room {request.RoomId} not found.");

        if (room.IsDm)
            throw new InvalidOperationException("Use the DM deletion path for direct message conversations.");

        if (room.IsProtected)
            throw new InvalidOperationException("The General topic cannot be deleted.");

        var isOwner = await _rooms.IsOwnerAsync(request.RoomId, request.RequestingUserId, cancellationToken);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the topic owner can delete this topic.");

        // Publish before deleting so SignalR can still broadcast to the room group
        await _eventBus.PublishAsync(new RoomDeletedIntegrationEvent
        {
            RoomId = request.RoomId,
        }, cancellationToken);

        await _rooms.DeleteAsync(request.RoomId, cancellationToken);
    }
}
