namespace Shared.Contracts.Events;

public sealed class RoomDeletedIntegrationEvent : IntegrationEvent
{
    public Guid RoomId { get; init; }
}
