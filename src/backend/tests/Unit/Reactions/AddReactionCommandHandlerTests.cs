using Bogus;
using Reactions.Application;
using Reactions.Application.Commands;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Reactions;

public class AddReactionCommandHandlerTests
{
    private readonly IReactionRepository _repo     = Substitute.For<IReactionRepository>();
    private readonly IEventBus           _eventBus = Substitute.For<IEventBus>();
    private static readonly Faker Fake = new();

    private AddReactionCommandHandler Build() => new(_repo, _eventBus);

    private static AddReactionCommand MakeCommand(
        Guid? messageId = null,
        Guid? userId    = null,
        string? emoji   = null) =>
        new(
            MessageId:   messageId ?? Guid.NewGuid(),
            RoomId:      Guid.NewGuid(),
            UserId:      userId    ?? Guid.NewGuid(),
            DisplayName: Fake.Internet.UserName(),
            Emoji:       emoji ?? "👍"
        );

    [Fact]
    public async Task Returns_added_true_and_count_from_repo()
    {
        var cmd = MakeCommand();
        _repo.ToggleAsync(cmd.MessageId, cmd.UserId, cmd.Emoji, Arg.Any<CancellationToken>())
             .Returns((Added: true, Count: 3, Users: (IReadOnlyList<string>)["Alice", "Bob", "Carol"]));
        _repo.GetMessageInfoAsync(cmd.MessageId, Arg.Any<CancellationToken>())
             .Returns((AuthorUserId: Guid.NewGuid(), MessageContent: "test", RoomName: "general"));

        var (added, count) = await Build().Handle(cmd, default);

        Assert.True(added);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Returns_added_false_when_reaction_removed()
    {
        var cmd = MakeCommand();
        _repo.ToggleAsync(cmd.MessageId, cmd.UserId, cmd.Emoji, Arg.Any<CancellationToken>())
             .Returns((Added: false, Count: 0, Users: (IReadOnlyList<string>)[]));
        _repo.GetMessageInfoAsync(cmd.MessageId, Arg.Any<CancellationToken>())
             .Returns((AuthorUserId: Guid.NewGuid(), MessageContent: "test", RoomName: "general"));

        var (added, _) = await Build().Handle(cmd, default);

        Assert.False(added);
    }

    [Fact]
    public async Task Publishes_reaction_updated_event_with_correct_fields()
    {
        var messageId  = Guid.NewGuid();
        var roomId     = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var authorId   = Guid.NewGuid();
        var reactors   = (IReadOnlyList<string>)["Alice"];
        var cmd        = new AddReactionCommand(messageId, roomId, userId, "Alice", "❤️");

        _repo.ToggleAsync(messageId, userId, "❤️", Arg.Any<CancellationToken>())
             .Returns((Added: true, Count: 1, Users: reactors));
        _repo.GetMessageInfoAsync(messageId, Arg.Any<CancellationToken>())
             .Returns((AuthorUserId: authorId, MessageContent: "great post", RoomName: "general"));

        await Build().Handle(cmd, default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ReactionUpdatedIntegrationEvent>(e =>
                e.MessageId          == messageId &&
                e.RoomId             == roomId    &&
                e.Emoji              == "❤️"       &&
                e.Count              == 1          &&
                e.Added              == true       &&
                e.ReactorUserId      == userId     &&
                e.AuthorUserId       == authorId   &&
                e.RoomName           == "general"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Content_preview_is_empty_when_message_not_found()
    {
        var cmd = MakeCommand();
        _repo.ToggleAsync(cmd.MessageId, cmd.UserId, cmd.Emoji, Arg.Any<CancellationToken>())
             .Returns((Added: true, Count: 1, Users: (IReadOnlyList<string>)[]));

        // Repo returns Guid.Empty when message not found (per IReactionRepository contract)
        _repo.GetMessageInfoAsync(cmd.MessageId, Arg.Any<CancellationToken>())
             .Returns((AuthorUserId: Guid.Empty, MessageContent: "", RoomName: ""));

        await Build().Handle(cmd, default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ReactionUpdatedIntegrationEvent>(e => e.ContentPreview == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Content_preview_truncates_long_messages_to_80_chars()
    {
        var cmd     = MakeCommand(emoji: "😂");
        var longMsg = new string('a', 200);
        _repo.ToggleAsync(cmd.MessageId, cmd.UserId, "😂", Arg.Any<CancellationToken>())
             .Returns((Added: true, Count: 1, Users: (IReadOnlyList<string>)[]));
        _repo.GetMessageInfoAsync(cmd.MessageId, Arg.Any<CancellationToken>())
             .Returns((AuthorUserId: Guid.NewGuid(), MessageContent: longMsg, RoomName: "general"));

        await Build().Handle(cmd, default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ReactionUpdatedIntegrationEvent>(e =>
                e.ContentPreview.Contains("😂") &&
                e.ContentPreview.Length <= 90),   // emoji + " — " + 80 chars
            Arg.Any<CancellationToken>());
    }
}
