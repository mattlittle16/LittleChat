using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed record MarkRoomReadCommand(Guid RoomId, Guid UserId) : IRequest<bool>;

public sealed class MarkRoomReadCommandHandler : IRequestHandler<MarkRoomReadCommand, bool>
{
    private readonly IRoomRepository _rooms;

    public MarkRoomReadCommandHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task<bool> Handle(MarkRoomReadCommand request, CancellationToken cancellationToken)
    {
        return await _rooms.MarkReadAsync(request.RoomId, request.UserId, cancellationToken);
    }
}
