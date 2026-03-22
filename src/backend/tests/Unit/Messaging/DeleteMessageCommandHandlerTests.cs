using Messaging.Application.Commands;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Messaging;

public class DeleteMessageCommandHandlerTests
{
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IEventBus          _eventBus = Substitute.For<IEventBus>();

    private DeleteMessageCommandHandler Build() => new(_messages, _eventBus);

    [Fact]
    public async Task Deletes_message_and_publishes_deleted_event()
    {
        var messageId = Guid.NewGuid();
        var roomId    = Guid.NewGuid();
        var userId    = Guid.NewGuid();

        var cmd = new DeleteMessageCommand(messageId, roomId, userId);
        await Build().Handle(cmd, default);

        await _messages.Received(1).DeleteAsync(messageId, userId, Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<MessageDeletedIntegrationEvent>(e =>
                e.MessageId == messageId && e.RoomId == roomId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_publish_event_if_repo_throws()
    {
        _messages.DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns<Task>(_ => Task.FromException(
                     new UnauthorizedAccessException("Cannot delete another user's message.")));

        var cmd = new DeleteMessageCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Build().Handle(cmd, default));

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<MessageDeletedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}
