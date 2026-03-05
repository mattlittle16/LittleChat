using MediatR;
using Notifications.Domain;
using Shared.Contracts.DTOs;

namespace Notifications.Application.Queries;

public sealed record GetOverridesQuery(Guid UserId) : IRequest<IReadOnlyList<ConversationOverrideDto>>;

public sealed class GetOverridesQueryHandler : IRequestHandler<GetOverridesQuery, IReadOnlyList<ConversationOverrideDto>>
{
    private readonly IConversationOverrideRepository _repo;

    public GetOverridesQueryHandler(IConversationOverrideRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ConversationOverrideDto>> Handle(GetOverridesQuery request, CancellationToken cancellationToken)
    {
        var overrides = await _repo.GetAllForUserAsync(request.UserId, cancellationToken);
        return overrides.Select(o => new ConversationOverrideDto(o.RoomId, o.Level)).ToList();
    }
}
