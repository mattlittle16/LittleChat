namespace Shared.Contracts.Events;

public sealed class ReactionUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public string Emoji { get; init; } = string.Empty;
    public int Count { get; init; }
    public bool Added { get; init; }
    public IReadOnlyList<string> Users { get; init; } = [];

    // Notification fields — populated by AddReactionCommandHandler for delivery to the message author
    public Guid ReactorUserId { get; init; }
    public string ReactorDisplayName { get; init; } = string.Empty;
    public Guid AuthorUserId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public string ContentPreview { get; init; } = string.Empty;
}
