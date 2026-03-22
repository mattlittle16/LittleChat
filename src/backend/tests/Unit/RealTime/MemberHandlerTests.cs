using Bogus;
using RealTime.Application.Handlers;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.RealTime;

public class MemberHandlerTests
{
    private readonly IRealtimeNotifier _notifier = Substitute.For<IRealtimeNotifier>();
    private static readonly Faker Fake = new();

    // ── MemberAddedHandler ──────────────────────────────────────────────────────

    [Fact]
    public async Task MemberAdded_sends_to_user_and_broadcasts_to_room()
    {
        var addedUserId = Guid.NewGuid();
        var roomId      = Guid.NewGuid();
        var evt         = new MemberAddedIntegrationEvent
        {
            RoomId      = roomId,
            AddedUserId = addedUserId,
            RoomName    = Fake.Commerce.Department(),
        };

        await new MemberAddedHandler(_notifier).HandleAsync(evt);

        await _notifier.Received(1).SendToUserAsync(
            addedUserId.ToString(),
            "RoomMembershipChanged",
            evt,
            Arg.Any<CancellationToken>());

        await _notifier.Received(1).BroadcastToRoomAsync(
            roomId.ToString(),
            "MemberListChanged",
            roomId,
            Arg.Any<CancellationToken>());
    }

    // ── MemberRemovedHandler ────────────────────────────────────────────────────

    [Fact]
    public async Task MemberRemoved_sends_to_user_and_broadcasts_to_room()
    {
        var removedUserId = Guid.NewGuid();
        var roomId        = Guid.NewGuid();
        var evt           = new MemberRemovedIntegrationEvent
        {
            RoomId        = roomId,
            RemovedUserId = removedUserId,
        };

        await new MemberRemovedHandler(_notifier).HandleAsync(evt);

        await _notifier.Received(1).SendToUserAsync(
            removedUserId.ToString(),
            "RemovedFromRoom",
            evt,
            Arg.Any<CancellationToken>());

        await _notifier.Received(1).BroadcastToRoomAsync(
            roomId.ToString(),
            "MemberListChanged",
            roomId,
            Arg.Any<CancellationToken>());
    }
}
