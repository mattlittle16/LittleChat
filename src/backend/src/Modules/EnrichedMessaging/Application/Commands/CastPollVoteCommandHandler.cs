using EnrichedMessaging.Domain;
using MediatR;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Application.Commands;

public sealed record CastPollVoteCommand(
    Guid PollId,
    Guid OptionId,
    Guid UserId,
    string DisplayName
) : IRequest<CastPollVoteResult>;

public sealed record CastPollVoteResult(
    Guid PollId,
    IReadOnlyList<PollOptionDto> Options,
    IReadOnlyList<Guid> CurrentUserVotedOptionIds
);

public sealed class CastPollVoteCommandHandler : IRequestHandler<CastPollVoteCommand, CastPollVoteResult>
{
    private readonly IPollRepository _polls;
    private readonly IEventBus _eventBus;

    public CastPollVoteCommandHandler(IPollRepository polls, IEventBus eventBus)
    {
        _polls = polls;
        _eventBus = eventBus;
    }

    public async Task<CastPollVoteResult> Handle(CastPollVoteCommand request, CancellationToken cancellationToken)
    {
        var roomId = await _polls.GetRoomIdByPollIdAsync(request.PollId, cancellationToken);
        if (roomId is null)
            throw new InvalidOperationException("Poll not found.");

        var isMember = await _polls.IsMemberAsync(roomId.Value, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        var updatedPoll = await _polls.AddVoteAsync(
            request.PollId, request.OptionId, request.UserId, request.DisplayName, cancellationToken);

        if (updatedPoll is null)
            throw new InvalidOperationException("Failed to process vote.");

        var optionDtos = updatedPoll.Options
            .Select(o => new PollOptionDto(o.Id, o.Text, o.DisplayOrder, o.VoteCount, o.VoterDisplayNames))
            .ToList();

        // Get current user's voted option IDs from the options
        var votedIds = updatedPoll.Options
            .Where(o => o.VoterDisplayNames.Contains(request.DisplayName))
            .Select(o => o.Id)
            .ToList();

        await _eventBus.PublishAsync(new PollVoteUpdatedIntegrationEvent
        {
            PollId    = request.PollId,
            MessageId = updatedPoll.MessageId,
            RoomId    = roomId.Value,
            Options   = optionDtos,
            CurrentUserVotedOptionIds = votedIds,
        }, cancellationToken);

        return new CastPollVoteResult(request.PollId, optionDtos, votedIds);
    }
}
