using Bogus;
using Messaging.Application.Commands;
using Messaging.Domain;

namespace Tests.Unit.Messaging;

public class CreateRoomCommandHandlerTests
{
    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private static readonly Faker Fake = new();

    private CreateRoomCommandHandler Build() => new(_rooms);

    private static Room StubRoom(Guid id, string name) =>
        new(id, name, IsDm: false, CreatedBy: Guid.NewGuid(), CreatedAt: DateTime.UtcNow);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Throws_when_room_name_is_empty(string name)
    {
        var cmd = new CreateRoomCommand(name, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Returns_created_room()
    {
        var roomId  = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var name    = Fake.Commerce.Department();
        var room    = StubRoom(roomId, name);
        _rooms.CreateAsync(name, creator, false, Arg.Any<CancellationToken>()).Returns(room);

        var result = await Build().Handle(new CreateRoomCommand(name, creator), default);

        Assert.Equal(roomId, result.Id);
        Assert.Equal(name,   result.Name);
    }

    [Fact]
    public async Task Adds_invitees_excluding_creator()
    {
        var creator  = Guid.NewGuid();
        var invitee1 = Guid.NewGuid();
        var invitee2 = Guid.NewGuid();
        var room     = StubRoom(Guid.NewGuid(), "test");
        _rooms.CreateAsync(Arg.Any<string>(), creator, false, Arg.Any<CancellationToken>()).Returns(room);

        var cmd = new CreateRoomCommand("test", creator, InvitedUserIds: [creator, invitee1, invitee2]);
        await Build().Handle(cmd, default);

        // Creator must NOT be added as an invitee
        await _rooms.DidNotReceive().AddMemberAsync(room.Id, creator, Arg.Any<CancellationToken>());
        await _rooms.Received(1).AddMemberAsync(room.Id, invitee1, Arg.Any<CancellationToken>());
        await _rooms.Received(1).AddMemberAsync(room.Id, invitee2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deduplicates_invitees()
    {
        var creator  = Guid.NewGuid();
        var invitee  = Guid.NewGuid();
        var room     = StubRoom(Guid.NewGuid(), "test");
        _rooms.CreateAsync(Arg.Any<string>(), creator, false, Arg.Any<CancellationToken>()).Returns(room);

        // Same invitee listed twice
        var cmd = new CreateRoomCommand("test", creator, InvitedUserIds: [invitee, invitee]);
        await Build().Handle(cmd, default);

        await _rooms.Received(1).AddMemberAsync(room.Id, invitee, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_member_calls_when_no_invitees()
    {
        var creator = Guid.NewGuid();
        var room    = StubRoom(Guid.NewGuid(), "test");
        _rooms.CreateAsync(Arg.Any<string>(), creator, false, Arg.Any<CancellationToken>()).Returns(room);

        await Build().Handle(new CreateRoomCommand("test", creator), default);

        await _rooms.DidNotReceive().AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
