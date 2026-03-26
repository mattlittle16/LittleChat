using Shared.Contracts.DTOs;

namespace Shared.Contracts.Events;

public sealed class PollVoteUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid PollId { get; init; }
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public IReadOnlyList<PollOptionDto> Options { get; init; } = [];
    public IReadOnlyList<Guid> CurrentUserVotedOptionIds { get; init; } = [];
}
