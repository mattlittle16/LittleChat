using MediatR;
using Notifications.Domain;
using Shared.Contracts.DTOs;

namespace Notifications.Application.Queries;

public sealed record GetNotificationsQuery(Guid UserId) : IRequest<IReadOnlyList<NotificationDto>>;

public sealed class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly IUserNotificationRepository _repo;

    public GetNotificationsQueryHandler(IUserNotificationRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await _repo.GetByRecipientAsync(request.UserId, cancellationToken);
        return notifications
            .Select(n => new NotificationDto(
                n.Id,
                n.RecipientUserId,
                n.Type,
                n.MessageId,
                n.RoomId,
                n.RoomName,
                n.FromUserId,
                n.FromDisplayName,
                n.ContentPreview,
                n.IsRead,
                n.CreatedAt,
                n.ExpiresAt))
            .ToList();
    }
}
