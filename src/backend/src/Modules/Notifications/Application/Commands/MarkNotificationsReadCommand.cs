using MediatR;
using Notifications.Domain;

namespace Notifications.Application.Commands;

public sealed record MarkNotificationsReadCommand(
    Guid UserId,
    IReadOnlyList<Guid>? NotificationIds,
    Guid? RoomId,
    bool All) : IRequest;

public sealed class MarkNotificationsReadCommandHandler : IRequestHandler<MarkNotificationsReadCommand>
{
    private readonly IUserNotificationRepository _repo;

    public MarkNotificationsReadCommandHandler(IUserNotificationRepository repo)
    {
        _repo = repo;
    }

    public async Task Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.All)
        {
            await _repo.MarkAllReadAsync(request.UserId, cancellationToken);
        }
        else if (request.RoomId.HasValue)
        {
            await _repo.MarkReadByRoomAsync(request.RoomId.Value, request.UserId, cancellationToken);
        }
        else if (request.NotificationIds is { Count: > 0 })
        {
            await _repo.MarkReadAsync(request.NotificationIds, request.UserId, cancellationToken);
        }
    }
}
