namespace Messaging.Domain;

public sealed record MessagePage(IReadOnlyList<Message> Messages, bool HasMore, bool HasNewer = false);

public interface IMessageRepository
{
    Task<Guid> CreateAsync(Message message, CancellationToken ct = default);
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MessagePage> GetPageAsync(Guid roomId, DateTime? before, Guid? beforeId, int limit, CancellationToken ct = default);
    Task<MessagePage> GetPageAroundAsync(Guid roomId, Guid aroundId, int limit, CancellationToken ct = default);
    Task<MessagePage> GetPageAfterAsync(Guid roomId, DateTime after, Guid afterId, int limit, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task UpdateLastReadAtAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task<string?> GetRoomNameAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Returns the new editedAt timestamp. Throws UnauthorizedAccessException if caller is not the author.</summary>
    Task<DateTime> EditAsync(Guid messageId, Guid userId, string newContent, CancellationToken ct = default);
    /// <summary>Hard-deletes the message. Throws UnauthorizedAccessException if caller is not the author.</summary>
    Task DeleteAsync(Guid messageId, Guid userId, CancellationToken ct = default);
}
