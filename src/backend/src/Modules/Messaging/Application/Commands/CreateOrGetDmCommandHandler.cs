using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed class CreateOrGetDmCommandHandler : IRequestHandler<CreateOrGetDmCommand, (Room Room, bool IsNew)>
{
    private readonly IRoomRepository _rooms;

    public CreateOrGetDmCommandHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
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

        return (room, IsNew: true);
    }
}
