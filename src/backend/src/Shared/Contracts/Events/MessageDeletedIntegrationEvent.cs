namespace Shared.Contracts.Events;

public sealed class MessageDeletedIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
}
