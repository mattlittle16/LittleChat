using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class MemberAddedHandler : IIntegrationEventHandler<MemberAddedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public MemberAddedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task HandleAsync(MemberAddedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        await _notifier.SendToUserAsync(
            evt.AddedUserId.ToString(),
            "RoomMembershipChanged",
            evt,
            cancellationToken);

        await _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "MemberListChanged",
            evt.RoomId,
            cancellationToken);
    }
}
