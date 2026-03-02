using Shared.Contracts.DTOs;

namespace Search.Application;

public interface IMessageSearchRepository
{
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(
        Guid userId,
        string q,
        string scope,
        Guid? roomId,
        CancellationToken ct = default);
}
