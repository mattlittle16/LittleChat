namespace Messaging.Domain;

public sealed record SidebarGroupInfo(Guid Id, string Name, int DisplayOrder, bool IsCollapsed, IReadOnlyList<Guid> RoomIds);

public interface ISidebarGroupRepository
{
    Task<IReadOnlyList<SidebarGroupInfo>> GetGroupsAsync(Guid userId, CancellationToken ct = default);
    Task<SidebarGroupInfo> CreateAsync(Guid userId, string name, CancellationToken ct = default);
    Task RenameAsync(Guid groupId, Guid userId, string name, CancellationToken ct = default);
    Task DeleteAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task AssignRoomAsync(Guid groupId, Guid userId, Guid roomId, CancellationToken ct = default);
    Task UnassignRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default);
    Task SetCollapsedAsync(Guid groupId, Guid userId, bool isCollapsed, CancellationToken ct = default);
}
