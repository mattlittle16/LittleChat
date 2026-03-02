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

        // GetAllRoomIdsAsync gives us existing IDs, but we need ALL user IDs here.
        // IRoomRepository.CreateAsync accepts allUserIds for bulk membership insert.
        var allUserIds = await _rooms.GetAllUserIdsAsync(cancellationToken);

        return await _rooms.CreateAsync(request.Name, request.CreatedBy, allUserIds, cancellationToken);
    }
}
