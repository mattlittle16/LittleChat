namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class RoomMembershipEntity
{
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public DateTime LastReadAt { get; set; }
    public DateTime JoinedAt { get; set; }

    public UserEntity User { get; set; } = null!;
    public RoomEntity Room { get; set; } = null!;
}
