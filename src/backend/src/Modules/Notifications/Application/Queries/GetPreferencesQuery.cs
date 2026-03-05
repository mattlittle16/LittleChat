using MediatR;
using Notifications.Domain;
using Shared.Contracts.DTOs;

namespace Notifications.Application.Queries;

public sealed record GetPreferencesQuery(Guid UserId) : IRequest<NotificationPreferencesDto>;

public sealed class GetPreferencesQueryHandler : IRequestHandler<GetPreferencesQuery, NotificationPreferencesDto>
{
    private readonly INotificationPreferencesRepository _repo;

    public GetPreferencesQueryHandler(INotificationPreferencesRepository repo)
    {
        _repo = repo;
    }

    public async Task<NotificationPreferencesDto> Handle(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var prefs = await _repo.GetAsync(request.UserId, cancellationToken);
        return new NotificationPreferencesDto(
            prefs.DmSoundEnabled,
            prefs.RoomSoundLevel,
            prefs.DndEnabled,
            prefs.BrowserNotificationsEnabled);
    }
}
