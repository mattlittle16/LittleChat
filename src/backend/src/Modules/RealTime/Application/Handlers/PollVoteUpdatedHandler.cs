using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class PollVoteUpdatedHandler : IIntegrationEventHandler<PollVoteUpdatedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public PollVoteUpdatedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task HandleAsync(PollVoteUpdatedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        await _notifier.BroadcastToRoomAsync(
            evt.RoomId.ToString(),
            "PollVoteUpdated",
            new
            {
                pollId   = evt.PollId,
                messageId = evt.MessageId,
                options  = evt.Options,
                currentUserVotedOptionIds = evt.CurrentUserVotedOptionIds,
            },
            cancellationToken);
    }
}
