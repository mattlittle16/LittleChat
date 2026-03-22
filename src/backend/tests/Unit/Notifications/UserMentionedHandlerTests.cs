using Bogus;
using Notifications.Application.Handlers;
using Notifications.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Notifications;

public class UserMentionedHandlerTests
{
    private readonly IRealtimeNotifier           _notifier = Substitute.For<IRealtimeNotifier>();
    private readonly IUserNotificationRepository _repo     = Substitute.For<IUserNotificationRepository>();
    private static readonly Faker Fake = new();

    private UserMentionedHandler Build() => new(_notifier, _repo);

    [Fact]
    public async Task Inserts_mention_notification_with_correct_fields()
    {
        var mentionedId = Guid.NewGuid();
        var fromId      = Guid.NewGuid();
        var roomId      = Guid.NewGuid();
        var messageId   = Guid.NewGuid();

        var evt = new MentionDetectedIntegrationEvent
        {
            MessageId       = messageId,
            RoomId          = roomId,
            RoomName        = Fake.Commerce.Department(),
            MentionedUserId = mentionedId,
            FromUserId      = fromId,
            FromDisplayName = Fake.Internet.UserName(),
            ContentPreview  = Fake.Lorem.Sentence(),
        };

        await Build().HandleAsync(evt);

        await _repo.Received(1).InsertAsync(
            Arg.Is<UserNotification>(n =>
                n.RecipientUserId == mentionedId &&
                n.Type            == NotificationType.Mention &&
                n.MessageId       == messageId &&
                n.RoomId          == roomId &&
                n.FromUserId      == fromId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sends_realtime_notification_to_mentioned_user()
    {
        var mentionedId = Guid.NewGuid();
        var evt         = new MentionDetectedIntegrationEvent
        {
            MessageId       = Guid.NewGuid(),
            RoomId          = Guid.NewGuid(),
            RoomName        = Fake.Commerce.Department(),
            MentionedUserId = mentionedId,
            FromUserId      = Guid.NewGuid(),
            FromDisplayName = Fake.Internet.UserName(),
            ContentPreview  = Fake.Lorem.Sentence(),
        };

        await Build().HandleAsync(evt);

        await _notifier.Received(1).SendToUserAsync(
            mentionedId.ToString(),
            "NotificationReceived",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }
}
