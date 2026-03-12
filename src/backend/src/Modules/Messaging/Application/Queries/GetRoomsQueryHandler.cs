using MediatR;
using Messaging.Domain;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, IReadOnlyList<RoomDto>>
{
    private readonly IRoomRepository _rooms;

    public GetRoomsQueryHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task<IReadOnlyList<RoomDto>> Handle(GetRoomsQuery request, CancellationToken cancellationToken)
    {
        var summaries = await _rooms.GetForUserAsync(request.UserId, cancellationToken);

        return summaries
            .Select(s => new RoomDto(
                Id:                   s.Room.Id,
                Name:                 s.Room.IsDm ? (s.OtherUserDisplayName ?? "Unknown") : s.Room.Name,
                IsDm:                 s.Room.IsDm,
                UnreadCount:          s.UnreadCount,
                HasMention:           s.HasMention,
                LastMessagePreview:   s.LastMessagePreview,
                CreatedAt:            s.Room.CreatedAt,
                IsPrivate:            s.Room.IsPrivate,
                OwnerId:              s.Room.OwnerId,
                IsProtected:          s.Room.IsProtected,
                MemberCount:          s.MemberCount,
                OtherUserId:          s.OtherUserId,
                OtherUserDisplayName: s.OtherUserDisplayName,
                OtherUserAvatarUrl:   s.OtherUserAvatarUrl))
            .ToList();
    }
}
