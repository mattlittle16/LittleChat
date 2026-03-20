using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed record GetAuditLogQuery(DateOnly? From, DateOnly? To, int Page, int PageSize) : IRequest<GetAuditLogResult>;

public sealed record GetAuditLogResult(
    IReadOnlyList<AuditLogEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record AuditLogEntryDto(
    long Id,
    Guid AdminId,
    string AdminName,
    string Action,
    string? TargetId,
    string? TargetName,
    DateTimeOffset OccurredAt);
