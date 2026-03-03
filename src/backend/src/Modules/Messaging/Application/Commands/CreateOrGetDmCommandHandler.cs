using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class CreateOrGetDmCommandHandler : IRequestHandler<CreateOrGetDmCommand, (Room Room, bool IsNew)>
{
    private readonly IRoomRepository _rooms;
    private readonly IEventBus _eventBus;

    public CreateOrGetDmCommandHandler(IRoomRepository rooms, IEventBus eventBus)
    {
        _rooms = rooms;
        _eventBus = eventBus;
    }

    public async Task<(Room Room, bool IsNew)> Handle(CreateOrGetDmCommand request, CancellationToken cancellationToken)
    {
        if (request.RequestingUserId == request.TargetUserId)
            throw new InvalidOperationException("Cannot create a DM with yourself.");

        var existingId = await _rooms.FindDmAsync(
            request.RequestingUserId, request.TargetUserId, cancellationToken);

        if (existingId.HasValue)
        {
            var existing = new Room(existingId.Value, string.Empty, IsDm: true, CreatedBy: null, CreatedAt: default);
            return (existing, IsNew: false);
        }

        var room = await _rooms.CreateDmAsync(
            request.RequestingUserId, request.TargetUserId, cancellationToken);

        // Notify the recipient that a new DM room has been created for them
        await _eventBus.PublishAsync(new DmCreatedIntegrationEvent
        {
            RoomId = room.Id,
            RecipientUserId = request.TargetUserId,
        }, cancellationToken);

        return (room, IsNew: true);
    }
}
