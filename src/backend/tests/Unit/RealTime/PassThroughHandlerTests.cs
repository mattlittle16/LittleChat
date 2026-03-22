using Bogus;
using RealTime.Application.Handlers;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.RealTime;

/// <summary>
/// Covers the thin pass-through handlers that simply call BroadcastToRoomAsync.
/// </summary>
public class PassThroughHandlerTests
{
    private readonly IRealtimeNotifier _notifier = Substitute.For<IRealtimeNotifier>();
    private static readonly Faker Fake = new();

    [Fact]
    public async Task MessageEdited_broadcasts_to_room()
    {
        var roomId = Guid.NewGuid();
        var evt    = new MessageEditedIntegrationEvent
        {
            RoomId    = roomId,
            MessageId = Guid.NewGuid(),
            Content   = Fake.Lorem.Sentence(),
            EditedAt  = DateTime.UtcNow,
        };

        await new MessageEditedHandler(_notifier).HandleAsync(evt);

        await _notifier.Received(1).BroadcastToRoomAsync(roomId.ToString(), "MessageEdited", evt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageDeleted_broadcasts_to_room()
    {
        var roomId = Guid.NewGuid();
        var evt    = new MessageDeletedIntegrationEvent { RoomId = roomId, MessageId = Guid.NewGuid() };

        await new MessageDeletedHandler(_notifier).HandleAsync(evt);

        await _notifier.Received(1).BroadcastToRoomAsync(roomId.ToString(), "MessageDeleted", evt, Arg.Any<CancellationToken>());
    }
}
