namespace Shared.Contracts.Events;

public sealed class LinkPreviewReadyIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public string Url { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool IsDismissed { get; init; }
}
