using MediatR;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Commands;

public sealed record CreateSidebarGroupCommand(Guid UserId, string Name) : IRequest<SidebarGroupDto>;
