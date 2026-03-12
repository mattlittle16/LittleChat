namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class SidebarGroupEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsCollapsed { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserEntity User { get; set; } = null!;
    public ICollection<RoomMembershipEntity> Memberships { get; set; } = [];
}
