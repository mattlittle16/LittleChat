namespace EnrichedMessaging.Infrastructure.Entities;

public sealed class PollVoteEntity
{
    public Guid Id { get; set; }
    public Guid PollId { get; set; }
    public Guid OptionId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public PollOptionEntity Option { get; set; } = null!;
}
