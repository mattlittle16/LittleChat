using MediatR;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed record GetRoomMembersQuery(Guid RoomId, Guid RequestingUserId) : IRequest<IReadOnlyList<RoomMemberDto>>;
