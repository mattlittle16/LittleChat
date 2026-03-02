using MediatR;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed record GetRoomsQuery(Guid UserId) : IRequest<IReadOnlyList<RoomDto>>;
