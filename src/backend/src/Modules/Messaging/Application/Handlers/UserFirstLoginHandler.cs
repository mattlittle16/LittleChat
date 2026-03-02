using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Handlers;

public sealed class UserFirstLoginHandler : IIntegrationEventHandler<UserFirstLoginIntegrationEvent>
{
    private readonly IRoomRepository _rooms;

    public UserFirstLoginHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task HandleAsync(UserFirstLoginIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var allRoomIds = await _rooms.GetAllRoomIdsAsync(cancellationToken);
        if (allRoomIds.Count == 0) return;

        // INSERT ... ON CONFLICT DO NOTHING — idempotent, handles race conditions
        await _rooms.AddMemberToAllRoomsAsync(integrationEvent.UserId, allRoomIds, cancellationToken);
    }
}
