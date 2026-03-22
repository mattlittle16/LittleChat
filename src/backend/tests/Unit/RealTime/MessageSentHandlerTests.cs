using Bogus;
using RealTime.Application.Handlers;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.RealTime;

public class MessageSentHandlerTests
{
    private readonly IRealtimeNotifier _notifier = Substitute.For<IRealtimeNotifier>();
    private static readonly Faker Fake = new();

    private MessageSentHandler Build() => new(_notifier);

    private static MessageSentIntegrationEvent MakeEvent(
        Guid? roomId      = null,
        Guid? userId      = null,
        bool isSystem     = false,
        IReadOnlyList<AttachmentEventData>? attachments = null) =>
        new()
        {
            MessageId   = Guid.NewGuid(),
            RoomId      = roomId  ?? Guid.NewGuid(),
            UserId      = userId  ?? Guid.NewGuid(),
            DisplayName = Fake.Internet.UserName(),
            Content     = Fake.Lorem.Sentence(),
            Attachments = attachments ?? [],
            CreatedAt   = DateTime.UtcNow,
            IsSystem    = isSystem,
        };

    [Fact]
    public async Task Broadcasts_ReceiveMessage_to_room()
    {
        var roomId = Guid.NewGuid();

        await Build().HandleAsync(MakeEvent(roomId: roomId));

        await _notifier.Received(1).BroadcastToRoomAsync(
            roomId.ToString(),
            "ReceiveMessage",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task System_message_sets_author_id_to_null_in_dto()
    {
        var captured = new List<MessageDto>();
        _notifier
            .BroadcastToRoomAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<MessageDto>(d => captured.Add(d)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Build().HandleAsync(MakeEvent(isSystem: true));

        Assert.Single(captured);
        Assert.Null(captured[0].Author.Id);
        Assert.True(captured[0].IsSystem);
    }

    [Fact]
    public async Task Non_system_message_includes_author_id_in_dto()
    {
        var userId   = Guid.NewGuid();
        var captured = new List<MessageDto>();
        _notifier
            .BroadcastToRoomAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<MessageDto>(d => captured.Add(d)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Build().HandleAsync(MakeEvent(userId: userId, isSystem: false));

        Assert.Single(captured);
        Assert.Equal(userId, captured[0].Author.Id);
        Assert.False(captured[0].IsSystem);
    }

    [Fact]
    public async Task Attachments_are_mapped_with_api_url()
    {
        var attachmentId = Guid.NewGuid();
        var fileName     = Fake.System.FileName("png");
        var captured     = new List<MessageDto>();
        _notifier
            .BroadcastToRoomAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<MessageDto>(d => captured.Add(d)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Build().HandleAsync(MakeEvent(attachments:
            [new AttachmentEventData(attachmentId, fileName, Fake.Random.Long(1, 5_000_000), "image/png", true)]));

        Assert.Single(captured[0].Attachments);
        Assert.Equal($"/api/files/attachments/{attachmentId}", captured[0].Attachments[0].Url);
    }
}
