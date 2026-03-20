namespace Shared.Contracts.Events;

public sealed class UserBannedIntegrationEvent : IntegrationEvent
{
    public Guid TargetUserId { get; init; }
    public string TargetDisplayName { get; init; } = string.Empty;
    public DateTimeOffset BannedUntil { get; init; }
}
