namespace Shared.Contracts.Events;

public sealed class HighlightChangedIntegrationEvent : IntegrationEvent
{
    public string Action { get; init; } = string.Empty; // "added" | "removed"
    public Guid HighlightId { get; init; }
    public Guid RoomId { get; init; }
    public Guid MessageId { get; init; }
    public string HighlightedByDisplayName { get; init; } = string.Empty;
    public DateTime HighlightedAt { get; init; }
    public string? ContentPreview { get; init; }
    public string? AuthorDisplayName { get; init; }
}
