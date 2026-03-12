using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed class DeleteSidebarGroupCommandHandler : IRequestHandler<DeleteSidebarGroupCommand>
{
    private readonly ISidebarGroupRepository _groups;

    public DeleteSidebarGroupCommandHandler(ISidebarGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task Handle(DeleteSidebarGroupCommand request, CancellationToken cancellationToken)
    {
        await _groups.DeleteAsync(request.GroupId, request.UserId, cancellationToken);
    }
}
