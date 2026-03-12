using MediatR;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed record DiscoverTopicsQuery(Guid UserId, string? SearchTerm = null)
    : IRequest<IReadOnlyList<DiscoverTopicDto>>;
