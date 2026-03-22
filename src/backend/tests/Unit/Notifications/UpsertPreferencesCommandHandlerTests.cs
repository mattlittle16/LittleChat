using Notifications.Application.Commands;
using Notifications.Domain;

namespace Tests.Unit.Notifications;

public class UpsertPreferencesCommandHandlerTests
{
    private readonly INotificationPreferencesRepository _repo = Substitute.For<INotificationPreferencesRepository>();

    private UpsertPreferencesCommandHandler Build() => new(_repo);

    private static NotificationPreferences DefaultPrefs(Guid userId) =>
        new(userId, DmSoundEnabled: true, RoomSoundLevel: "mentions_only", DndEnabled: false, BrowserNotificationsEnabled: true);

    [Theory]
    [InlineData("loud")]
    [InlineData("MUTED")]
    public async Task Throws_for_invalid_room_sound_level(string level)
    {
        var cmd = new UpsertPreferencesCommand(Guid.NewGuid(), null, level, null, null);

        await Assert.ThrowsAsync<ArgumentException>(() => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Null_level_skips_validation_and_upserts()
    {
        var userId = Guid.NewGuid();
        _repo.GetAsync(userId, Arg.Any<CancellationToken>()).Returns(DefaultPrefs(userId));

        var cmd = new UpsertPreferencesCommand(userId, null, null, null, null);

        await Build().Handle(cmd, default);  // should not throw

        await _repo.Received(1).UpsertAsync(Arg.Any<NotificationPreferences>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Applies_partial_update_keeping_unchanged_fields()
    {
        var userId = Guid.NewGuid();
        var prefs  = DefaultPrefs(userId);  // DmSoundEnabled=true, DndEnabled=false
        _repo.GetAsync(userId, Arg.Any<CancellationToken>()).Returns(prefs);

        // Only changing DndEnabled
        var cmd = new UpsertPreferencesCommand(userId, null, null, DndEnabled: true, null);

        await Build().Handle(cmd, default);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<NotificationPreferences>(p =>
                p.DmSoundEnabled              == true &&          // unchanged
                p.RoomSoundLevel              == "mentions_only" && // unchanged
                p.DndEnabled                  == true &&          // updated
                p.BrowserNotificationsEnabled == true),           // unchanged
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updates_all_provided_fields()
    {
        var userId = Guid.NewGuid();
        _repo.GetAsync(userId, Arg.Any<CancellationToken>()).Returns(DefaultPrefs(userId));

        var cmd = new UpsertPreferencesCommand(userId, false, "muted", true, false);

        await Build().Handle(cmd, default);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<NotificationPreferences>(p =>
                p.DmSoundEnabled              == false  &&
                p.RoomSoundLevel              == "muted" &&
                p.DndEnabled                  == true   &&
                p.BrowserNotificationsEnabled == false),
            Arg.Any<CancellationToken>());
    }
}
