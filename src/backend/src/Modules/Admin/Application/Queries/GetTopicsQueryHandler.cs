using LittleChat.Modules.Admin.Domain;
using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed class GetTopicsQueryHandler : IRequestHandler<GetTopicsQuery, GetTopicsResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;

    public GetTopicsQueryHandler(IAdminRepository repo, IAuditLogRepository auditLog)
    {
        _repo = repo;
        _auditLog = auditLog;
    }

    public async Task<GetTopicsResult> Handle(GetTopicsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repo.GetTopicsAsync(request.Search, request.Page, request.PageSize, cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        return new GetTopicsResult(items, totalCount, request.Page, request.PageSize, totalPages);
    }
}
