namespace Shared.Contracts.Events;

public sealed class DmCreatedIntegrationEvent : IntegrationEvent
{
    public Guid RoomId { get; init; }
    public Guid RecipientUserId { get; init; }
}
