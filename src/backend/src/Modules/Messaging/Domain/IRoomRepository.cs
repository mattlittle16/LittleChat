namespace Messaging.Domain;

public interface IRoomRepository
{
    Task<IReadOnlyList<Guid>> GetAllRoomIdsAsync(CancellationToken cancellationToken = default);
    Task AddMemberToAllRoomsAsync(Guid userId, IReadOnlyList<Guid> roomIds, CancellationToken cancellationToken = default);
}
