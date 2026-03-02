namespace Shared.Contracts.Events;

public sealed class UserFirstLoginIntegrationEvent : IntegrationEvent
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
