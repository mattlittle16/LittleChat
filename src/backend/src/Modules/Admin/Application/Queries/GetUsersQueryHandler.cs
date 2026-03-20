using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, GetUsersResult>
{
    private readonly IAdminRepository _repo;
    private readonly ITokenBlocklistService _blocklist;

    public GetUsersQueryHandler(IAdminRepository repo, ITokenBlocklistService blocklist)
    {
        _repo = repo;
        _blocklist = blocklist;
    }

    public async Task<GetUsersResult> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repo.GetUsersAsync(request.Search, request.Page, request.PageSize, cancellationToken);

        var expiries = await Task.WhenAll(items.Select(u => _blocklist.GetBanExpiryAsync(u.Id, cancellationToken)));
        var itemsWithBan = items.Zip(expiries, (u, expiry) => u with { BannedUntil = expiry }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        return new GetUsersResult(itemsWithBan, totalCount, request.Page, request.PageSize, totalPages);
    }
}
