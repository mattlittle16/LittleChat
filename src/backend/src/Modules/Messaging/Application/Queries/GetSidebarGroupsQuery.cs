using MediatR;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed record GetSidebarGroupsQuery(Guid UserId) : IRequest<IReadOnlyList<SidebarGroupDto>>;
