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
        // New users join only the General room (the protected default topic)
        await _rooms.AddMemberToGeneralRoomAsync(integrationEvent.UserId, cancellationToken);
    }
}
