using Bogus;
using Notifications.Application.Handlers;
using Notifications.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Notifications;

public class TopicAlertHandlerTests
{
    private readonly IRealtimeNotifier           _notifier = Substitute.For<IRealtimeNotifier>();
    private readonly IUserNotificationRepository _repo     = Substitute.For<IUserNotificationRepository>();
    private static readonly Faker Fake = new();

    private TopicAlertHandler Build() => new(_notifier, _repo);

    private static TopicAlertIntegrationEvent MakeEvent(
        Guid? roomId           = null,
        IReadOnlyList<Guid>? recipients = null) =>
        new()
        {
            MessageId         = Guid.NewGuid(),
            RoomId            = roomId ?? Guid.NewGuid(),
            RoomName          = Fake.Commerce.Department(),
            SenderUserId      = Guid.NewGuid(),
            SenderDisplayName = Fake.Internet.UserName(),
            ContentPreview    = Fake.Lorem.Sentence(),
            RecipientUserIds  = recipients ?? [],
        };

    [Fact]
    public async Task Does_nothing_when_no_recipients()
    {
        await Build().HandleAsync(MakeEvent(recipients: []));

        await _repo.DidNotReceive().InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inserts_notification_and_notifies_each_recipient()
    {
        var r1  = Guid.NewGuid();
        var r2  = Guid.NewGuid();
        var r3  = Guid.NewGuid();
        var evt = MakeEvent(recipients: [r1, r2, r3]);

        await Build().HandleAsync(evt);

        await _repo.Received(3).InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).SendToUserAsync(r1.ToString(), "NotificationReceived", Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).SendToUserAsync(r2.ToString(), "NotificationReceived", Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).SendToUserAsync(r3.ToString(), "NotificationReceived", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_notification_has_correct_recipient_and_type()
    {
        var r1     = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await Build().HandleAsync(MakeEvent(roomId: roomId, recipients: [r1]));

        await _repo.Received(1).InsertAsync(
            Arg.Is<UserNotification>(n =>
                n.RecipientUserId == r1 &&
                n.Type            == NotificationType.TopicAlert &&
                n.RoomId          == roomId),
            Arg.Any<CancellationToken>());
    }
}
