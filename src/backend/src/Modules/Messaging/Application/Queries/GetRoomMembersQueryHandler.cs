using MediatR;
using Messaging.Domain;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed class GetRoomMembersQueryHandler : IRequestHandler<GetRoomMembersQuery, IReadOnlyList<RoomMemberDto>>
{
    private readonly IRoomRepository _rooms;

    public GetRoomMembersQueryHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task<IReadOnlyList<RoomMemberDto>> Handle(GetRoomMembersQuery request, CancellationToken cancellationToken)
    {
        var isMember = await _rooms.IsMemberAsync(request.RoomId, request.RequestingUserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("You are not a member of this topic.");

        var members = await _rooms.GetMembersAsync(request.RoomId, cancellationToken);
        return members.Select(m => new RoomMemberDto(m.UserId, m.DisplayName, m.AvatarUrl)).ToList();
    }
}
