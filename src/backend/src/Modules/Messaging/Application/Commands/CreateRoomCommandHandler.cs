using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, Room>
{
    private readonly IRoomRepository _rooms;

    public CreateRoomCommandHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task<Room> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Room name cannot be empty.");

        var room = await _rooms.CreateAsync(request.Name, request.CreatedBy, request.IsPrivate, cancellationToken);

        // Bulk-add any initial invitees (skip creator, deduplicate, ignore already-members)
        if (request.InvitedUserIds is { Count: > 0 })
        {
            foreach (var inviteeId in request.InvitedUserIds.Distinct().Where(id => id != request.CreatedBy))
                await _rooms.AddMemberAsync(room.Id, inviteeId, cancellationToken);
        }

        return room;
    }
}
