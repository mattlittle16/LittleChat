using Bogus;
using Notifications.Application.Handlers;
using Notifications.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Notifications;

public class DmUnreadNotificationHandlerTests
{
    private readonly IRealtimeNotifier           _notifier = Substitute.For<IRealtimeNotifier>();
    private readonly IUserNotificationRepository _repo     = Substitute.For<IUserNotificationRepository>();
    private static readonly Faker Fake = new();

    private DmUnreadNotificationHandler Build() => new(_notifier, _repo);

    [Fact]
    public async Task Inserts_unread_dm_notification_with_correct_recipient()
    {
        var recipientId = Guid.NewGuid();
        var senderId    = Guid.NewGuid();
        var evt = new DmMessageSentIntegrationEvent
        {
            MessageId         = Guid.NewGuid(),
            RoomId            = Guid.NewGuid(),
            RoomName          = Fake.Internet.UserName(),
            SenderUserId      = senderId,
            SenderDisplayName = Fake.Internet.UserName(),
            ContentPreview    = Fake.Lorem.Sentence(),
            RecipientUserId   = recipientId,
        };

        await Build().HandleAsync(evt);

        await _repo.Received(1).InsertAsync(
            Arg.Is<UserNotification>(n =>
                n.RecipientUserId == recipientId &&
                n.Type            == NotificationType.UnreadDm &&
                n.FromUserId      == senderId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sends_realtime_notification_to_recipient()
    {
        var recipientId = Guid.NewGuid();
        var evt = new DmMessageSentIntegrationEvent
        {
            MessageId         = Guid.NewGuid(),
            RoomId            = Guid.NewGuid(),
            RoomName          = Fake.Internet.UserName(),
            SenderUserId      = Guid.NewGuid(),
            SenderDisplayName = Fake.Internet.UserName(),
            ContentPreview    = Fake.Lorem.Sentence(),
            RecipientUserId   = recipientId,
        };

        await Build().HandleAsync(evt);

        await _notifier.Received(1).SendToUserAsync(
            recipientId.ToString(),
            "NotificationReceived",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }
}
