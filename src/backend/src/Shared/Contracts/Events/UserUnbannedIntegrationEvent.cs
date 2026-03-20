namespace Shared.Contracts.Events;

public sealed class UserUnbannedIntegrationEvent : IntegrationEvent
{
    public Guid TargetUserId { get; init; }
    public string TargetDisplayName { get; init; } = string.Empty;
}
