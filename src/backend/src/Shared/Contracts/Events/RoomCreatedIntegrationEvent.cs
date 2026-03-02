namespace Shared.Contracts.Events;

public sealed class RoomCreatedIntegrationEvent : IntegrationEvent
{
    public Guid RoomId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsDm { get; init; }
    public Guid CreatedBy { get; init; }
}
