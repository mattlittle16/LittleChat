using MediatR;
using Notifications.Domain;

namespace Notifications.Application.Commands;

// All fields nullable — only provided fields are applied (partial update via server merge)
public sealed record UpsertPreferencesCommand(
    Guid UserId,
    bool? DmSoundEnabled,
    string? RoomSoundLevel,
    bool? DndEnabled,
    bool? BrowserNotificationsEnabled
) : IRequest;

public sealed class UpsertPreferencesCommandHandler : IRequestHandler<UpsertPreferencesCommand>
{
    private static readonly HashSet<string> ValidLevels = ["all_messages", "mentions_only", "muted"];
    private readonly INotificationPreferencesRepository _repo;

    public UpsertPreferencesCommandHandler(INotificationPreferencesRepository repo)
    {
        _repo = repo;
    }

    public async Task Handle(UpsertPreferencesCommand request, CancellationToken cancellationToken)
    {
        if (request.RoomSoundLevel is not null && !ValidLevels.Contains(request.RoomSoundLevel))
            throw new ArgumentException($"Invalid roomSoundLevel: {request.RoomSoundLevel}");

        var current = await _repo.GetAsync(request.UserId, cancellationToken);

        var updated = current with
        {
            DmSoundEnabled = request.DmSoundEnabled ?? current.DmSoundEnabled,
            RoomSoundLevel = request.RoomSoundLevel ?? current.RoomSoundLevel,
            DndEnabled = request.DndEnabled ?? current.DndEnabled,
            BrowserNotificationsEnabled = request.BrowserNotificationsEnabled ?? current.BrowserNotificationsEnabled,
        };

        await _repo.UpsertAsync(updated, cancellationToken);
    }
}
