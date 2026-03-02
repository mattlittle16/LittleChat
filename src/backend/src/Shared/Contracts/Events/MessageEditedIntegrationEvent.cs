namespace Shared.Contracts.Events;

public sealed class MessageEditedIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime EditedAt { get; init; }
}
