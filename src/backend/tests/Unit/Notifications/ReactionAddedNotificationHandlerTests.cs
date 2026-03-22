using Bogus;
using Notifications.Application.Handlers;
using Notifications.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Notifications;

public class ReactionAddedNotificationHandlerTests
{
    private readonly IRealtimeNotifier           _notifier = Substitute.For<IRealtimeNotifier>();
    private readonly IUserNotificationRepository _repo     = Substitute.For<IUserNotificationRepository>();
    private static readonly Faker Fake = new();

    private ReactionAddedNotificationHandler Build() => new(_notifier, _repo);

    private static ReactionUpdatedIntegrationEvent MakeEvent(
        bool added         = true,
        Guid? reactorId    = null,
        Guid? authorId     = null) =>
        new()
        {
            MessageId          = Guid.NewGuid(),
            RoomId             = Guid.NewGuid(),
            Emoji              = "👍",
            Count              = Fake.Random.Int(1, 10),
            Added              = added,
            ReactorUserId      = reactorId ?? Guid.NewGuid(),
            ReactorDisplayName = Fake.Internet.UserName(),
            AuthorUserId       = authorId  ?? Guid.NewGuid(),
            ContentPreview     = Fake.Lorem.Sentence(),
            RoomName           = Fake.Commerce.Department(),
        };

    [Fact]
    public async Task Does_not_notify_when_reaction_removed()
    {
        await Build().HandleAsync(MakeEvent(added: false));

        await _repo.DidNotReceive().InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_notify_for_self_reaction()
    {
        var userId = Guid.NewGuid();
        await Build().HandleAsync(MakeEvent(reactorId: userId, authorId: userId));

        await _repo.DidNotReceive().InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_notify_when_author_is_empty_guid()
    {
        await Build().HandleAsync(MakeEvent(authorId: Guid.Empty));

        await _repo.DidNotReceive().InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inserts_notification_and_sends_realtime_on_valid_reaction()
    {
        var authorId  = Guid.NewGuid();
        var reactorId = Guid.NewGuid();
        var evt       = MakeEvent(added: true, reactorId: reactorId, authorId: authorId);

        await Build().HandleAsync(evt);

        await _repo.Received(1).InsertAsync(
            Arg.Is<UserNotification>(n =>
                n.RecipientUserId == authorId &&
                n.Type            == NotificationType.Reaction &&
                n.FromUserId      == reactorId),
            Arg.Any<CancellationToken>());

        await _notifier.Received(1).SendToUserAsync(
            authorId.ToString(),
            "NotificationReceived",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }
}
