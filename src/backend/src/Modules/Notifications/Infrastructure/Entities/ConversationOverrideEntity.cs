namespace Notifications.Infrastructure.Entities;

public sealed class ConversationOverrideEntity
{
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
