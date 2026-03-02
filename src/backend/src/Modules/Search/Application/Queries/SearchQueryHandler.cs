using MediatR;
using Shared.Contracts.DTOs;

namespace Search.Application.Queries;

public sealed class SearchQueryHandler : IRequestHandler<SearchQuery, IReadOnlyList<SearchResultDto>>
{
    private readonly IMessageSearchRepository _repo;

    public SearchQueryHandler(IMessageSearchRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<SearchResultDto>> Handle(
        SearchQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Q))
            return Array.Empty<SearchResultDto>();

        if (request.Scope == "room" && request.RoomId is null)
            throw new ArgumentException("RoomId is required for scoped search.");

        return await _repo.SearchAsync(
            request.UserId,
            request.Q,
            request.Scope,
            request.RoomId,
            cancellationToken);
    }
}
