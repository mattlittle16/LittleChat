namespace Messaging.Domain;

public interface IRoomRepository
{
    // US1
    Task<IReadOnlyList<Guid>> GetAllRoomIdsAsync(CancellationToken ct = default);
    Task AddMemberToAllRoomsAsync(Guid userId, IReadOnlyList<Guid> roomIds, CancellationToken ct = default);

    // US3
    Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken ct = default);
    Task<Room> CreateAsync(string name, Guid createdBy, IReadOnlyList<Guid> allUserIds, CancellationToken ct = default);
    Task<IReadOnlyList<RoomSummary>> GetForUserAsync(Guid userId, CancellationToken ct = default);

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
}
