namespace Messaging.Domain;

public sealed record RoomMember(Guid UserId, string DisplayName, string? AvatarUrl);
public sealed record DiscoverTopicResult(Guid Id, string Name, int MemberCount, DateTime CreatedAt);

public interface IRoomRepository
{
    // 012-topics-overhaul: create a room (creator-only membership)
    Task<Room> CreateAsync(string name, Guid createdBy, bool isPrivate = false, CancellationToken ct = default);
    Task<IReadOnlyList<RoomSummary>> GetForUserAsync(Guid userId, CancellationToken ct = default);

    // 012-topics-overhaul: add new user to the General (protected) room only
    Task AddMemberToGeneralRoomAsync(Guid userId, CancellationToken ct = default);

    // US2 / Phase 4 — membership management
    Task<IReadOnlyList<RoomMember>> GetMembersAsync(Guid roomId, CancellationToken ct = default);
    Task AddMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task SetOwnerAsync(Guid roomId, Guid newOwnerId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task<bool> IsOwnerAsync(Guid roomId, Guid userId, CancellationToken ct = default);
    Task<Guid?> GetOwnerIdAsync(Guid roomId, CancellationToken ct = default);
    Task<int> GetMemberCountAsync(Guid roomId, CancellationToken ct = default);

    // US4 — DMs
    Task<Guid?> FindDmAsync(Guid userA, Guid userB, CancellationToken ct = default);
    Task<Room> CreateDmAsync(Guid userA, Guid userB, CancellationToken ct = default);

    // 002-fix-dm-emoji-bugs: US1 — used by ChatHub.OnConnectedAsync to join all room groups
    Task<IReadOnlyList<Guid>> GetRoomIdsForUserAsync(Guid userId, CancellationToken ct = default);

    // 002-fix-dm-emoji-bugs: US2 — used by DeleteDmCommandHandler
    Task<Room?> GetByIdAsync(Guid roomId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetRoomMemberIdsAsync(Guid roomId, CancellationToken ct = default);
    Task DeleteAsync(Guid roomId, CancellationToken ct = default);

    // 005-notification-settings: US3 — persist last-read position
    Task<bool> MarkReadAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    // 012-topics-overhaul: US3 — discover public topics the user hasn't joined
    Task<IReadOnlyList<DiscoverTopicResult>> DiscoverTopicsAsync(Guid userId, string? searchTerm, CancellationToken ct = default);
}
