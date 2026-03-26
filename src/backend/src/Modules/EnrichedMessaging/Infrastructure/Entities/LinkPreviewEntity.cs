namespace EnrichedMessaging.Infrastructure.Entities;

public sealed class LinkPreviewEntity
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime FetchedAt { get; set; }
}
