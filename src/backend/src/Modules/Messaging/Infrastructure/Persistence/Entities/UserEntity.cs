namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ProfileImagePath { get; set; }
    public float? CropX { get; set; }
    public float? CropY { get; set; }
    public float? CropZoom { get; set; }
    public string OnboardingStatus { get; set; } = "not_started";

    public ICollection<RoomMembershipEntity> Memberships { get; set; } = [];
    public ICollection<MessageEntity> Messages { get; set; } = [];
}
