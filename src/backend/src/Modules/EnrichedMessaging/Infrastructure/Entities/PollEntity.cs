namespace EnrichedMessaging.Infrastructure.Entities;

public sealed class PollEntity
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string VoteMode { get; set; } = "single";
    public DateTime CreatedAt { get; set; }

    public ICollection<PollOptionEntity> Options { get; set; } = [];
}
