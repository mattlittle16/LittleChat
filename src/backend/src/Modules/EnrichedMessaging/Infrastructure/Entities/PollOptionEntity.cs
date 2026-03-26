namespace EnrichedMessaging.Infrastructure.Entities;

public sealed class PollOptionEntity
{
    public Guid Id { get; set; }
    public Guid PollId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public PollEntity Poll { get; set; } = null!;
    public ICollection<PollVoteEntity> Votes { get; set; } = [];
}
