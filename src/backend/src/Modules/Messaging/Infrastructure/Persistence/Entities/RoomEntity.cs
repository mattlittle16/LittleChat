namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class RoomEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDm { get; set; }
    public string Visibility { get; set; } = "public";
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<RoomMembershipEntity> Memberships { get; set; } = [];
    public ICollection<MessageEntity> Messages { get; set; } = [];
}
