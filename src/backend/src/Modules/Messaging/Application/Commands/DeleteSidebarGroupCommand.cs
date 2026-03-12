using MediatR;

namespace Messaging.Application.Commands;

public sealed record DeleteSidebarGroupCommand(Guid GroupId, Guid UserId) : IRequest;
