namespace Messaging.Domain;

public sealed record MessagePage(IReadOnlyList<Message> Messages, bool HasMore);

public interface IMessageRepository
{
    Task<Guid> CreateAsync(Message message, CancellationToken ct = default);
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MessagePage> GetPageAsync(Guid roomId, DateTime? before, Guid? beforeId, int limit, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task UpdateLastReadAtAsync(Guid roomId, Guid userId, CancellationToken ct = default);
}
