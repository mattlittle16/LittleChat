namespace EnrichedMessaging.Infrastructure.Entities;

public sealed class HighlightEntity
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid MessageId { get; set; }
    public Guid HighlightedByUserId { get; set; }
    public string HighlightedByDisplayName { get; set; } = string.Empty;
    public DateTime HighlightedAt { get; set; }
}
