using MediatR;

namespace Messaging.Application.Commands;

public sealed record RenameSidebarGroupCommand(Guid GroupId, Guid UserId, string Name) : IRequest;
