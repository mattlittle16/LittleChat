using EnrichedMessaging.Domain;
using MediatR;

namespace EnrichedMessaging.Application.Queries;

public sealed record GetHighlightsQuery(Guid RoomId, Guid UserId) : IRequest<IReadOnlyList<Highlight>>;

public sealed class GetHighlightsQueryHandler : IRequestHandler<GetHighlightsQuery, IReadOnlyList<Highlight>>
{
    private readonly IHighlightRepository _highlights;

    public GetHighlightsQueryHandler(IHighlightRepository highlights)
    {
        _highlights = highlights;
    }

    public async Task<IReadOnlyList<Highlight>> Handle(GetHighlightsQuery request, CancellationToken cancellationToken)
    {
        var isMember = await _highlights.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        return await _highlights.GetByRoomAsync(request.RoomId, cancellationToken);
    }
}
