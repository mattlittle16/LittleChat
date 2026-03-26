using NpgsqlTypes;

namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class MessageEntity
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid? UserId { get; set; }
    public bool IsSystem { get; set; }
    public string? AuthorDisplayName { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }

    public string MessageType { get; set; } = "text";
    public Guid? QuotedMessageId { get; set; }
    public string? QuotedAuthorDisplayName { get; set; }
    public string? QuotedContentSnapshot { get; set; }

    public RoomEntity Room { get; set; } = null!;
    public UserEntity? User { get; set; }
    public ICollection<ReactionEntity> Reactions { get; set; } = [];
    public ICollection<MessageAttachmentEntity> Attachments { get; set; } = [];
}
