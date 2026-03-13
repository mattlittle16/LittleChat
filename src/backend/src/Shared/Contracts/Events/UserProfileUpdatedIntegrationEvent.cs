namespace Shared.Contracts.Events;

public sealed class UserProfileUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
}
