using Notifications.Application.Commands;
using Notifications.Domain;

namespace Tests.Unit.Notifications;

public class SetOverrideCommandHandlerTests
{
    private readonly IConversationOverrideRepository _repo = Substitute.For<IConversationOverrideRepository>();

    private SetOverrideCommandHandler Build() => new(_repo);

    [Theory]
    [InlineData("loud")]
    [InlineData("")]
    [InlineData("ALL_MESSAGES")]
    public async Task Throws_for_invalid_level(string level)
    {
        var cmd = new SetOverrideCommand(Guid.NewGuid(), Guid.NewGuid(), level);

        await Assert.ThrowsAsync<ArgumentException>(() => Build().Handle(cmd, default));
        await _repo.DidNotReceive().UpsertAsync(Arg.Any<ConversationNotificationOverride>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("all_messages")]
    [InlineData("mentions_only")]
    [InlineData("muted")]
    public async Task Upserts_override_for_valid_level(string level)
    {
        var userId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var cmd    = new SetOverrideCommand(userId, roomId, level);

        await Build().Handle(cmd, default);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<ConversationNotificationOverride>(o =>
                o.UserId == userId &&
                o.RoomId == roomId &&
                o.Level  == level),
            Arg.Any<CancellationToken>());
    }
}
