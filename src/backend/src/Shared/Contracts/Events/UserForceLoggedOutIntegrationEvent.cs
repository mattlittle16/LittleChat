namespace Shared.Contracts.Events;

public sealed class UserForceLoggedOutIntegrationEvent : IntegrationEvent
{
    public Guid TargetUserId { get; init; }
}
