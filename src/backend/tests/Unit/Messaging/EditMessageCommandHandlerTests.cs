using Bogus;
using Messaging.Application.Commands;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Messaging;

public class EditMessageCommandHandlerTests
{
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IEventBus          _eventBus = Substitute.For<IEventBus>();
    private static readonly Faker Fake = new();

    private EditMessageCommandHandler Build() => new(_messages, _eventBus);

    // ── Validation ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Throws_when_content_is_empty(string content)
    {
        var cmd = new EditMessageCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), content);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Build().Handle(cmd, default));
    }

    [Fact]
    public async Task Throws_when_content_exceeds_4000_chars()
    {
        var cmd = new EditMessageCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('x', 4001));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Build().Handle(cmd, default));
    }

    // ── Happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Calls_repo_edit_and_publishes_edited_event()
    {
        var messageId = Guid.NewGuid();
        var roomId    = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var newContent = Fake.Lorem.Sentence();
        var editedAt  = DateTime.UtcNow;

        _messages.EditAsync(messageId, userId, newContent.Trim(), Arg.Any<CancellationToken>())
                 .Returns(editedAt);

        var cmd = new EditMessageCommand(messageId, roomId, userId, newContent);
        await Build().Handle(cmd, default);

        await _messages.Received(1).EditAsync(messageId, userId, newContent.Trim(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<MessageEditedIntegrationEvent>(e =>
                e.MessageId == messageId &&
                e.RoomId    == roomId    &&
                e.EditedAt  == editedAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Trims_content_before_persisting()
    {
        var messageId  = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var paddedContent = "  hello world  ";
        _messages.EditAsync(messageId, userId, "hello world", Arg.Any<CancellationToken>())
                 .Returns(DateTime.UtcNow);

        var cmd = new EditMessageCommand(messageId, Guid.NewGuid(), userId, paddedContent);
        await Build().Handle(cmd, default);

        await _messages.Received(1).EditAsync(messageId, userId, "hello world", Arg.Any<CancellationToken>());
    }
}
