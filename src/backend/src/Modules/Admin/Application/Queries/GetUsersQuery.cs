using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed record GetUsersQuery(string? Search, int Page, int PageSize) : IRequest<GetUsersResult>;

public sealed record GetUsersResult(
    IReadOnlyList<AdminUserDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
