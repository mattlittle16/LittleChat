using Bogus;
using Messaging.Application.Commands;
using Messaging.Domain;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Messaging;

public class SendMessageCommandHandlerTests
{
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IRoomRepository    _rooms    = Substitute.For<IRoomRepository>();
    private readonly IEventBus          _eventBus = Substitute.For<IEventBus>();
    private readonly IUserLookupService _lookup   = Substitute.For<IUserLookupService>();
    private readonly ILogger<SendMessageCommandHandler> _logger =
        Substitute.For<ILogger<SendMessageCommandHandler>>();

    private static readonly Faker Fake = new();

    private SendMessageCommandHandler Build() =>
        new(_messages, _rooms, _eventBus, _logger, userLookup: _lookup);

    private static SendMessageCommand TextCommand(
        Guid? roomId  = null,
        Guid? userId  = null,
        string? content = null) =>
        new(
            MessageId:         Guid.NewGuid(),
            RoomId:            roomId  ?? Guid.NewGuid(),
            UserId:            userId  ?? Guid.NewGuid(),
            AuthorDisplayName: Fake.Internet.UserName(),
            AuthorAvatarUrl:   null,
            Content:           content ?? Fake.Lorem.Sentence(),
            Files:             []
        );

    // ── Guard rails ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Throws_when_content_empty_and_no_files()
    {
        var cmd = TextCommand(content: "   ");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Throws_when_content_exceeds_4000_chars()
    {
        var cmd = TextCommand(content: new string('x', 4001));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Throws_when_user_is_not_a_room_member()
    {
        _messages.IsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Build().Handle(TextCommand(), default));
    }

    // ── Happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Persists_before_broadcasting_constitutionPrincipleI()
    {
        var cmd = TextCommand();
        _messages.IsMemberAsync(cmd.RoomId, cmd.UserId, Arg.Any<CancellationToken>())
                 .Returns(true);

        // Track call order via side-effects
        var callOrder = new List<string>();
        _messages.CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
                 .Returns(ci => { callOrder.Add("persist"); return ci.Arg<Message>().Id; });
        _eventBus.PublishAsync(Arg.Any<MessageSentIntegrationEvent>(), Arg.Any<CancellationToken>())
                 .Returns(ci => { callOrder.Add("broadcast"); return Task.CompletedTask; });

        // No room needed for this simple text-only, non-DM test
        _rooms.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns((Room?)null);

        await Build().Handle(cmd, default);

        Assert.Equal(["persist", "broadcast"], callOrder);
    }

    [Fact]
    public async Task Returns_message_id_on_success()
    {
        var cmd = TextCommand();
        _messages.IsMemberAsync(cmd.RoomId, cmd.UserId, Arg.Any<CancellationToken>())
                 .Returns(true);
        _messages.CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
                 .Returns(cmd.MessageId);
        _rooms.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns((Room?)null);

        var result = await Build().Handle(cmd, default);

        Assert.Equal(cmd.MessageId, result.MessageId);
        Assert.Empty(result.FailedFileNames);
    }

    // ── @mention detection ─────────────────────────────────────────────────────

    [Fact]
    public async Task Publishes_mention_event_when_valid_user_mentioned()
    {
        var mentionedId = Guid.NewGuid();
        var sender      = Guid.NewGuid();
        var cmd         = TextCommand(userId: sender, content: "@Alice hello there");

        _messages.IsMemberAsync(cmd.RoomId, sender, Arg.Any<CancellationToken>()).Returns(true);
        _messages.CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>()).Returns(cmd.MessageId);
        _messages.GetRoomNameAsync(cmd.RoomId, Arg.Any<CancellationToken>()).Returns("general");
        _lookup.FindIdByDisplayNameAsync("Alice", Arg.Any<CancellationToken>()).Returns(mentionedId);
        _rooms.GetByIdAsync(cmd.RoomId, Arg.Any<CancellationToken>())
              .Returns(new Room(cmd.RoomId, "general", IsDm: false, CreatedBy: sender, CreatedAt: DateTime.UtcNow));
        _rooms.GetRoomMemberIdsAsync(cmd.RoomId, Arg.Any<CancellationToken>())
              .Returns((IReadOnlyList<Guid>)[sender, mentionedId]);

        await Build().Handle(cmd, default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<MentionDetectedIntegrationEvent>(e =>
                e.MentionedUserId == mentionedId && e.FromUserId == sender),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_publish_mention_event_for_self_mention()
    {
        var userId = Guid.NewGuid();
        var cmd    = TextCommand(userId: userId, content: "@Self look at this");

        _messages.IsMemberAsync(cmd.RoomId, userId, Arg.Any<CancellationToken>()).Returns(true);
        _messages.CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>()).Returns(cmd.MessageId);
        _lookup.FindIdByDisplayNameAsync("Self", Arg.Any<CancellationToken>()).Returns(userId); // same user
        _rooms.GetByIdAsync(cmd.RoomId, Arg.Any<CancellationToken>())
              .Returns(new Room(cmd.RoomId, "general", IsDm: false, CreatedBy: userId, CreatedAt: DateTime.UtcNow));
        _rooms.GetRoomMemberIdsAsync(cmd.RoomId, Arg.Any<CancellationToken>())
              .Returns((IReadOnlyList<Guid>)[userId]);

        await Build().Handle(cmd, default);

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<MentionDetectedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    // ── @topic alert ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Publishes_topic_alert_when_at_topic_used_in_topic_room()
    {
        var sender    = Guid.NewGuid();
        var memberId  = Guid.NewGuid();
        var cmd       = TextCommand(userId: sender, content: "Hey @topic check this out");
        var room      = new Room(cmd.RoomId, "engineering", IsDm: false,
                            CreatedBy: sender, CreatedAt: DateTime.UtcNow);

        _messages.IsMemberAsync(cmd.RoomId, sender, Arg.Any<CancellationToken>()).Returns(true);
        _messages.CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>()).Returns(cmd.MessageId);
        _messages.GetRoomNameAsync(cmd.RoomId, Arg.Any<CancellationToken>()).Returns("engineering");
        _rooms.GetByIdAsync(cmd.RoomId, Arg.Any<CancellationToken>()).Returns(room);
        _rooms.GetRoomMemberIdsAsync(cmd.RoomId, Arg.Any<CancellationToken>())
              .Returns((IReadOnlyList<Guid>)[sender, memberId]);

        await Build().Handle(cmd, default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<TopicAlertIntegrationEvent>(e =>
                e.RoomId == cmd.RoomId &&
                e.SenderUserId == sender &&
                e.RecipientUserIds.Contains(memberId) &&
                !e.RecipientUserIds.Contains(sender)),
            Arg.Any<CancellationToken>());
    }

    // ── DM notification ────────────────────────────────────────────────────────

    [Fact]
    public async Task Publishes_dm_sent_event_for_dm_room()
    {
        var sender    = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var cmd       = TextCommand(userId: sender, content: "Hey!");
        var dmRoom    = new Room(cmd.RoomId, string.Empty, IsDm: true,
                            CreatedBy: sender, CreatedAt: DateTime.UtcNow);

        _messages.IsMemberAsync(cmd.RoomId, sender, Arg.Any<CancellationToken>()).Returns(true);
        _messages.CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>()).Returns(cmd.MessageId);
        _messages.GetRoomNameAsync(cmd.RoomId, Arg.Any<CancellationToken>()).Returns(string.Empty);
        _rooms.GetByIdAsync(cmd.RoomId, Arg.Any<CancellationToken>()).Returns(dmRoom);
        _rooms.GetRoomMemberIdsAsync(cmd.RoomId, Arg.Any<CancellationToken>())
              .Returns((IReadOnlyList<Guid>)[sender, recipient]);

        await Build().Handle(cmd, default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<DmMessageSentIntegrationEvent>(e =>
                e.RecipientUserId == recipient && e.SenderUserId == sender),
            Arg.Any<CancellationToken>());
    }
}
