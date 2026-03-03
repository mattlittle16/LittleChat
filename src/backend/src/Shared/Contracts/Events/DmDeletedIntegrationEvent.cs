namespace Shared.Contracts.Events;

public sealed class DmDeletedIntegrationEvent : IntegrationEvent
{
    public Guid RoomId { get; init; }
}
