namespace Shared.Contracts.Events;

public sealed class MemberRemovedIntegrationEvent : IntegrationEvent
{
    public Guid RoomId { get; init; }
    public Guid RemovedUserId { get; init; }
}
