namespace Shared.Contracts.Events;

public sealed class ReactionUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public string Emoji { get; init; } = string.Empty;
    public int Count { get; init; }
    public bool Added { get; init; }
    public IReadOnlyList<string> Users { get; init; } = [];
}
