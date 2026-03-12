using MediatR;
using Messaging.Domain;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed class GetSidebarGroupsQueryHandler : IRequestHandler<GetSidebarGroupsQuery, IReadOnlyList<SidebarGroupDto>>
{
    private readonly ISidebarGroupRepository _groups;

    public GetSidebarGroupsQueryHandler(ISidebarGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task<IReadOnlyList<SidebarGroupDto>> Handle(GetSidebarGroupsQuery request, CancellationToken cancellationToken)
    {
        var groups = await _groups.GetGroupsAsync(request.UserId, cancellationToken);

        return groups
            .Select(g => new SidebarGroupDto(g.Id, g.Name, g.DisplayOrder, g.IsCollapsed, g.RoomIds))
            .ToList();
    }
}
