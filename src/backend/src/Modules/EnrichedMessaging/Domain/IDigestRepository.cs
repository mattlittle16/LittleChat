namespace EnrichedMessaging.Domain;

public sealed record DigestMessageRaw(
    Guid Id,
    Guid RoomId,
    string RoomName,
    Guid? UserId,
    string DisplayName,
    string? AvatarUrl,
    string Content,
    string MessageType,
    DateTime CreatedAt,
    Guid? QuotedMessageId,
    string? QuotedAuthorDisplayName,
    string? QuotedContentSnapshot
);

public interface IDigestRepository
{
    Task<IReadOnlyList<DigestMessageRaw>> GetMessagesAsync(
        Guid userId, DateTime start, DateTime end, CancellationToken ct = default);
}
