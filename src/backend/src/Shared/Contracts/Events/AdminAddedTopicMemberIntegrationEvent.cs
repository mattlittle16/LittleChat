namespace Shared.Contracts.Events;

public sealed class AdminAddedTopicMemberIntegrationEvent : IntegrationEvent
{
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
