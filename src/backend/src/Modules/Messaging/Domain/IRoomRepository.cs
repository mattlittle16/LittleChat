namespace Messaging.Domain;

public interface IRoomRepository
{
    // Used by UserFirstLoginHandler (US1)
    Task<IReadOnlyList<Guid>> GetAllRoomIdsAsync(CancellationToken ct = default);
    Task AddMemberToAllRoomsAsync(Guid userId, IReadOnlyList<Guid> roomIds, CancellationToken ct = default);

    // Used by US3
    Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken ct = default);
    Task<Room> CreateAsync(string name, Guid createdBy, IReadOnlyList<Guid> allUserIds, CancellationToken ct = default);
    Task<IReadOnlyList<RoomSummary>> GetForUserAsync(Guid userId, CancellationToken ct = default);
}
