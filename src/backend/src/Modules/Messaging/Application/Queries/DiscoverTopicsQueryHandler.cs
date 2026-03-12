using MediatR;
using Messaging.Domain;
using Shared.Contracts.DTOs;

namespace Messaging.Application.Queries;

public sealed class DiscoverTopicsQueryHandler : IRequestHandler<DiscoverTopicsQuery, IReadOnlyList<DiscoverTopicDto>>
{
    private readonly IRoomRepository _rooms;

    public DiscoverTopicsQueryHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task<IReadOnlyList<DiscoverTopicDto>> Handle(DiscoverTopicsQuery request, CancellationToken cancellationToken)
    {
        var results = await _rooms.DiscoverTopicsAsync(request.UserId, request.SearchTerm, cancellationToken);

        return results
            .Select(r => new DiscoverTopicDto(r.Id, r.Name, r.MemberCount, r.CreatedAt))
            .ToList();
    }
}
