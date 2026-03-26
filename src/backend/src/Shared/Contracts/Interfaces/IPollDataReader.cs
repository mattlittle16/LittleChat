using Shared.Contracts.DTOs;

namespace Shared.Contracts.Interfaces;

public interface IPollDataReader
{
    Task<IReadOnlyDictionary<Guid, PollDataDto>> GetForMessagesAsync(
        IReadOnlyList<Guid> messageIds, Guid currentUserId, CancellationToken ct = default);
}
