using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed record DeleteDmCommand(Guid RoomId, Guid RequestingUserId) : IRequest;

public sealed class DeleteDmCommandHandler : IRequestHandler<DeleteDmCommand>
{
    private readonly IRoomRepository _rooms;
    private readonly IEventBus _eventBus;

    public DeleteDmCommandHandler(IRoomRepository rooms, IEventBus eventBus)
    {
        _rooms = rooms;
        _eventBus = eventBus;
    }

    public async Task Handle(DeleteDmCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(request.RoomId, cancellationToken)
            ?? throw new KeyNotFoundException($"Room {request.RoomId} not found.");

        if (!room.IsDm)
            throw new InvalidOperationException("Only DM conversations can be deleted.");

        var memberIds = await _rooms.GetRoomMemberIdsAsync(request.RoomId, cancellationToken);
        if (!memberIds.Contains(request.RequestingUserId))
            throw new UnauthorizedAccessException("You are not a member of this conversation.");

        // Publish before deleting so SignalR can still broadcast to the room group
        await _eventBus.PublishAsync(new DmDeletedIntegrationEvent
        {
            RoomId = request.RoomId,
        }, cancellationToken);

        await _rooms.DeleteAsync(request.RoomId, cancellationToken);
    }
}
