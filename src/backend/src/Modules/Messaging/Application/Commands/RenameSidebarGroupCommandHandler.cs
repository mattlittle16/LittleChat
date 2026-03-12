using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed class RenameSidebarGroupCommandHandler : IRequestHandler<RenameSidebarGroupCommand>
{
    private readonly ISidebarGroupRepository _groups;

    public RenameSidebarGroupCommandHandler(ISidebarGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task Handle(RenameSidebarGroupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 50)
            throw new ArgumentException("Group name must be between 1 and 50 characters.");

        await _groups.RenameAsync(request.GroupId, request.UserId, request.Name.Trim(), cancellationToken);
    }
}
