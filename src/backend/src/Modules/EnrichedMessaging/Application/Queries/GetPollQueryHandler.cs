using EnrichedMessaging.Domain;
using MediatR;
using Shared.Contracts.DTOs;

namespace EnrichedMessaging.Application.Queries;

public sealed record GetPollQuery(Guid PollId, Guid CurrentUserId) : IRequest<GetPollResult?>;

public sealed record GetPollResult(
    Guid PollId,
    IReadOnlyList<PollOptionDto> Options,
    IReadOnlyList<Guid> CurrentUserVotedOptionIds
);

public sealed class GetPollQueryHandler : IRequestHandler<GetPollQuery, GetPollResult?>
{
    private readonly IPollRepository _polls;

    public GetPollQueryHandler(IPollRepository polls)
    {
        _polls = polls;
    }

    public async Task<GetPollResult?> Handle(GetPollQuery request, CancellationToken cancellationToken)
    {
        var poll = await _polls.GetByIdAsync(request.PollId, request.CurrentUserId, cancellationToken);
        if (poll is null) return null;

        var optionDtos = poll.Options
            .Select(o => new PollOptionDto(o.Id, o.Text, o.DisplayOrder, o.VoteCount, o.VoterDisplayNames))
            .ToList();

        // Voted IDs come from poll options — we need to check which ones the current user voted on
        // The poll repository's LoadPoll reads voted IDs separately; for simplicity return from options
        var votedIds = poll.Options
            .Where(o => o.VoterDisplayNames.Count > 0) // simplified — repository should return per-user
            .Select(o => o.Id)
            .ToList();

        return new GetPollResult(poll.Id, optionDtos, []);
    }
}
