using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Queries;

public sealed record GetUserRoomIdsQuery(Guid UserId) : IRequest<IReadOnlyList<Guid>>;

public sealed class GetUserRoomIdsQueryHandler : IRequestHandler<GetUserRoomIdsQuery, IReadOnlyList<Guid>>
{
    private readonly IRoomRepository _rooms;

    public GetUserRoomIdsQueryHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public Task<IReadOnlyList<Guid>> Handle(GetUserRoomIdsQuery request, CancellationToken cancellationToken)
        => _rooms.GetRoomIdsForUserAsync(request.UserId, cancellationToken);
}
