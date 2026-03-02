namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class ReactionEntity
{
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public MessageEntity Message { get; set; } = null!;
    public UserEntity User { get; set; } = null!;
}
