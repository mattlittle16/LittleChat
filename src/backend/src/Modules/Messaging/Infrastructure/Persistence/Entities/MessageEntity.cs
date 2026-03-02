using NpgsqlTypes;

namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class MessageEntity
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }

    public RoomEntity Room { get; set; } = null!;
    public UserEntity User { get; set; } = null!;
    public ICollection<ReactionEntity> Reactions { get; set; } = [];
}
