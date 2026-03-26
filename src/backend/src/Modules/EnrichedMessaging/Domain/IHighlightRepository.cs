namespace EnrichedMessaging.Domain;

public interface IHighlightRepository
{
    Task<IReadOnlyList<Highlight>> GetByRoomAsync(Guid roomId, CancellationToken ct = default);
    Task<Highlight?> AddAsync(Guid roomId, Guid messageId, Guid userId, string displayName, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid highlightId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task<bool> MessageExistsInRoomAsync(Guid messageId, Guid roomId, CancellationToken ct = default);
}
