namespace EnrichedMessaging.Domain;

public interface IPollRepository
{
    Task<Poll?> GetByIdAsync(Guid pollId, Guid currentUserId, CancellationToken ct = default);
    Task<Poll?> GetByMessageIdAsync(Guid messageId, Guid currentUserId, CancellationToken ct = default);
    Task CreatePollMessageAsync(Guid messageId, Guid roomId, Guid userId, string question, DateTime now, CancellationToken ct = default);
    Task CreateAsync(Poll poll, IReadOnlyList<PollOption> options, CancellationToken ct = default);
    Task<Poll?> AddVoteAsync(Guid pollId, Guid optionId, Guid userId, string displayName, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task<Guid?> GetRoomIdByPollIdAsync(Guid pollId, CancellationToken ct = default);
    Task DeleteByMessageIdAsync(Guid messageId, CancellationToken ct = default);
}
