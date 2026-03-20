using LittleChat.Modules.Admin.Domain;
using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, GetAuditLogResult>
{
    private readonly IAuditLogRepository _auditLog;

    public GetAuditLogQueryHandler(IAuditLogRepository auditLog)
    {
        _auditLog = auditLog;
    }

    public async Task<GetAuditLogResult> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _auditLog.GetAsync(request.From, request.To, request.Page, request.PageSize, cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var dtos = items.Select(e => new AuditLogEntryDto(
            e.Id, e.AdminId, e.AdminName, e.Action, e.TargetId, e.TargetName, e.OccurredAt))
            .ToList();

        return new GetAuditLogResult(dtos, totalCount, request.Page, request.PageSize, totalPages);
    }
}
