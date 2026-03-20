using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed record GetTopicsQuery(string? Search, int Page, int PageSize) : IRequest<GetTopicsResult>;

public sealed record GetTopicsResult(
    IReadOnlyList<AdminTopicDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
