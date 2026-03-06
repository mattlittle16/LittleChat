namespace Messaging.Domain;

public sealed record Message(
    Guid Id,
    Guid RoomId,
    Guid UserId,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Content,
    string? FilePath,
    string? FileName,
    long? FileSize,
    DateTime CreatedAt,
    DateTime? EditedAt,
    DateTime ExpiresAt,
    IReadOnlyList<MessageReaction> Reactions
);

public sealed record MessageReaction(string Emoji, string UserDisplayName);
