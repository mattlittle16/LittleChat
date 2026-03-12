namespace Shared.Contracts.Events;

public sealed class MemberAddedIntegrationEvent : IntegrationEvent
{
    public Guid RoomId { get; init; }
    public Guid AddedUserId { get; init; }
    public string RoomName { get; init; } = string.Empty;
}
