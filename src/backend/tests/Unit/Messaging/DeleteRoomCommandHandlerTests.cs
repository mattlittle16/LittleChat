using Messaging.Application.Commands;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Messaging;

public class DeleteRoomCommandHandlerTests
{
    private readonly IRoomRepository _rooms    = Substitute.For<IRoomRepository>();
    private readonly IEventBus       _eventBus = Substitute.For<IEventBus>();

    private DeleteRoomCommandHandler Build() => new(_rooms, _eventBus);

    private static Room MakeRoom(
        Guid? id = null, bool isDm = false, bool isProtected = false, Guid? ownerId = null) =>
        new(
            Id:          id ?? Guid.NewGuid(),
            Name:        "test-room",
            IsDm:        isDm,
            CreatedBy:   Guid.NewGuid(),
            CreatedAt:   DateTime.UtcNow,
            OwnerId:     ownerId,
            IsPrivate:   false,
            IsProtected: isProtected
        );

    [Fact]
    public async Task Throws_when_room_not_found()
    {
        _rooms.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns((Room?)null);

        var cmd = new DeleteRoomCommand(Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Throws_when_room_is_dm()
    {
        var room = MakeRoom(isDm: true);
        _rooms.GetByIdAsync(room.Id, Arg.Any<CancellationToken>()).Returns(room);

        var cmd = new DeleteRoomCommand(room.Id, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Throws_when_room_is_protected()
    {
        var room = MakeRoom(isProtected: true);
        _rooms.GetByIdAsync(room.Id, Arg.Any<CancellationToken>()).Returns(room);

        var cmd = new DeleteRoomCommand(room.Id, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Throws_when_requesting_user_is_not_owner()
    {
        var room = MakeRoom();
        _rooms.GetByIdAsync(room.Id, Arg.Any<CancellationToken>()).Returns(room);
        _rooms.IsOwnerAsync(room.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var cmd = new DeleteRoomCommand(room.Id, Guid.NewGuid());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Publishes_event_before_deleting_constitutionPrincipleI()
    {
        var owner = Guid.NewGuid();
        var room  = MakeRoom(ownerId: owner);
        _rooms.GetByIdAsync(room.Id, Arg.Any<CancellationToken>()).Returns(room);
        _rooms.IsOwnerAsync(room.Id, owner, Arg.Any<CancellationToken>()).Returns(true);

        var callOrder = new List<string>();
        _eventBus.PublishAsync(Arg.Any<RoomDeletedIntegrationEvent>(), Arg.Any<CancellationToken>())
                 .Returns(ci => { callOrder.Add("event"); return Task.CompletedTask; });
        _rooms.DeleteAsync(room.Id, Arg.Any<CancellationToken>())
              .Returns(ci => { callOrder.Add("delete"); return Task.CompletedTask; });

        await Build().Handle(new DeleteRoomCommand(room.Id, owner), default);

        Assert.Equal(["event", "delete"], callOrder);
    }

    [Fact]
    public async Task Deletes_room_after_publishing_event()
    {
        var owner = Guid.NewGuid();
        var room  = MakeRoom(ownerId: owner);
        _rooms.GetByIdAsync(room.Id, Arg.Any<CancellationToken>()).Returns(room);
        _rooms.IsOwnerAsync(room.Id, owner, Arg.Any<CancellationToken>()).Returns(true);

        await Build().Handle(new DeleteRoomCommand(room.Id, owner), default);

        await _rooms.Received(1).DeleteAsync(room.Id, Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<RoomDeletedIntegrationEvent>(e => e.RoomId == room.Id),
            Arg.Any<CancellationToken>());
    }
}
