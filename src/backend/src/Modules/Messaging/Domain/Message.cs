namespace Messaging.Domain;

public sealed record Message(
    Guid Id,
    Guid RoomId,
    Guid? UserId,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Content,
    IReadOnlyList<MessageAttachment> Attachments,
    DateTime CreatedAt,
    DateTime? EditedAt,
    DateTime ExpiresAt,
    IReadOnlyList<MessageReaction> Reactions,
    bool IsSystem = false
);

public sealed record MessageReaction(string Emoji, string UserDisplayName);
