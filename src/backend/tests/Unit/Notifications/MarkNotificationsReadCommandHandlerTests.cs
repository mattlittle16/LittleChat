using Notifications.Application.Commands;
using Notifications.Domain;

namespace Tests.Unit.Notifications;

public class MarkNotificationsReadCommandHandlerTests
{
    private readonly IUserNotificationRepository _repo = Substitute.For<IUserNotificationRepository>();

    private MarkNotificationsReadCommandHandler Build() => new(_repo);

    [Fact]
    public async Task All_flag_calls_MarkAllReadAsync()
    {
        var userId = Guid.NewGuid();
        var cmd    = new MarkNotificationsReadCommand(userId, null, null, All: true);

        await Build().Handle(cmd, default);

        await _repo.Received(1).MarkAllReadAsync(userId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().MarkReadByRoomAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().MarkReadAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoomId_calls_MarkReadByRoomAsync()
    {
        var userId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var cmd    = new MarkNotificationsReadCommand(userId, null, roomId, All: false);

        await Build().Handle(cmd, default);

        await _repo.Received(1).MarkReadByRoomAsync(roomId, userId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().MarkAllReadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationIds_calls_MarkReadAsync()
    {
        var userId = Guid.NewGuid();
        var ids    = (IReadOnlyList<Guid>)[Guid.NewGuid(), Guid.NewGuid()];
        var cmd    = new MarkNotificationsReadCommand(userId, ids, null, All: false);

        await Build().Handle(cmd, default);

        await _repo.Received(1).MarkReadAsync(ids, userId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().MarkAllReadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_ids_list_does_not_call_any_repo_method()
    {
        var cmd = new MarkNotificationsReadCommand(Guid.NewGuid(), [], null, All: false);

        await Build().Handle(cmd, default);

        await _repo.DidNotReceive().MarkReadAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().MarkAllReadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().MarkReadByRoomAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task All_flag_takes_precedence_over_RoomId()
    {
        var userId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var cmd    = new MarkNotificationsReadCommand(userId, null, roomId, All: true);

        await Build().Handle(cmd, default);

        await _repo.Received(1).MarkAllReadAsync(userId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().MarkReadByRoomAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
