using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed class SetGroupCollapsedCommandHandler : IRequestHandler<SetGroupCollapsedCommand>
{
    private readonly ISidebarGroupRepository _groups;

    public SetGroupCollapsedCommandHandler(ISidebarGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task Handle(SetGroupCollapsedCommand request, CancellationToken cancellationToken)
    {
        await _groups.SetCollapsedAsync(request.GroupId, request.UserId, request.IsCollapsed, cancellationToken);
    }
}
