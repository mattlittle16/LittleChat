using MediatR;
using Shared.Contracts.DTOs;

namespace Search.Application.Queries;

public sealed record SearchQuery(
    Guid UserId,
    string Q,
    string Scope,   // "room" | "global"
    Guid? RoomId)   // required when Scope = "room"
    : IRequest<IReadOnlyList<SearchResultDto>>;
