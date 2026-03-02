namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<RoomMembershipEntity> Memberships { get; set; } = [];
    public ICollection<MessageEntity> Messages { get; set; } = [];
}
