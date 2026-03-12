using MediatR;

namespace Messaging.Application.Commands;

public sealed record SetGroupCollapsedCommand(Guid GroupId, Guid UserId, bool IsCollapsed) : IRequest;
