namespace Shared.Contracts.Events;

public sealed class UserStatusUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid UserId { get; init; }
    public string? Emoji { get; init; }
    public string? Text { get; init; }
    public string? Color { get; init; }
}
