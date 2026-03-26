namespace EnrichedMessaging.Domain;

public interface ILinkPreviewRepository
{
    Task<LinkPreview?> GetByMessageIdAsync(Guid messageId, CancellationToken ct = default);
    Task UpsertAsync(LinkPreview preview, CancellationToken ct = default);
    Task<bool> DismissAsync(Guid messageId, Guid userId, CancellationToken ct = default);
    Task<Guid?> GetRoomIdByMessageAndUserAsync(Guid messageId, Guid userId, CancellationToken ct = default);
}
