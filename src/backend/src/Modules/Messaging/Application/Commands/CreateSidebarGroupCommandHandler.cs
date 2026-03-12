using MediatR;
using Messaging.Domain;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Commands;

public sealed class CreateSidebarGroupCommandHandler : IRequestHandler<CreateSidebarGroupCommand, SidebarGroupDto>
{
    private readonly ISidebarGroupRepository _groups;

    public CreateSidebarGroupCommandHandler(ISidebarGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task<SidebarGroupDto> Handle(CreateSidebarGroupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 50)
            throw new ArgumentException("Group name must be between 1 and 50 characters.");

        var group = await _groups.CreateAsync(request.UserId, request.Name.Trim(), cancellationToken);

        return new SidebarGroupDto(group.Id, group.Name, group.DisplayOrder, group.IsCollapsed, group.RoomIds);
    }
}
