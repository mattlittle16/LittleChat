using Shared.Contracts.DTOs;

namespace Shared.Contracts.Interfaces;

public interface ILinkPreviewReader
{
    Task<IReadOnlyDictionary<Guid, LinkPreviewDataDto>> GetForMessagesAsync(
        IReadOnlyList<Guid> messageIds, CancellationToken ct = default);
}
