using Messaging.Domain;
using Messaging.Infrastructure.Persistence;
using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Infrastructure;

public sealed class SidebarGroupRepository : ISidebarGroupRepository
{
    private readonly LittleChatDbContext _db;

    public SidebarGroupRepository(LittleChatDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SidebarGroupInfo>> GetGroupsAsync(Guid userId, CancellationToken ct = default)
    {
        // Two separate server-side queries joined in C#.
        // Previously, .ToList() inside Select() forced EF Core to load all memberships
        // into memory before filtering client-side. Now both queries are fully server-side.
        var groups = await _db.SidebarGroups
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.DisplayOrder)
            .Select(g => new { g.Id, g.Name, g.DisplayOrder, g.IsCollapsed })
            .ToListAsync(ct);

        var memberships = await _db.RoomMemberships
            .Where(m => m.UserId == userId && m.SidebarGroupId != null)
            .OrderBy(m => m.Position)
            .ThenBy(m => m.Room.Name)
            .Select(m => new { m.SidebarGroupId, m.RoomId })
            .ToListAsync(ct);

        var membershipsByGroup = memberships
            .GroupBy(m => m.SidebarGroupId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.RoomId).ToList());

        return groups.Select(g => new SidebarGroupInfo(
            g.Id,
            g.Name,
            g.DisplayOrder,
            g.IsCollapsed,
            membershipsByGroup.GetValueOrDefault(g.Id) ?? [])).ToList();
    }

    public async Task<SidebarGroupInfo> CreateAsync(Guid userId, string name, CancellationToken ct = default)
    {
        var maxOrder = await _db.SidebarGroups
            .Where(g => g.UserId == userId)
            .Select(g => (int?)g.DisplayOrder)
            .MaxAsync(ct) ?? -1;

        var entity = new SidebarGroupEntity
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            Name         = name,
            DisplayOrder = maxOrder + 1,
            IsCollapsed  = false,
            CreatedAt    = DateTime.UtcNow,
        };

        _db.SidebarGroups.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new SidebarGroupInfo(entity.Id, entity.Name, entity.DisplayOrder, entity.IsCollapsed, []);
    }

    public async Task RenameAsync(Guid groupId, Guid userId, string name, CancellationToken ct = default)
    {
        var rows = await _db.SidebarGroups
            .Where(g => g.Id == groupId && g.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Name, name), ct);

        if (rows == 0)
            throw new KeyNotFoundException("Sidebar group not found.");
    }

    public async Task DeleteAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        // Un-assign all rooms first (SetNull on cascade handles this, but explicit is safer)
        await _db.RoomMemberships
            .Where(m => m.UserId == userId && m.SidebarGroupId == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.SidebarGroupId, (Guid?)null), ct);

        var rows = await _db.SidebarGroups
            .Where(g => g.Id == groupId && g.UserId == userId)
            .ExecuteDeleteAsync(ct);

        if (rows == 0)
            throw new KeyNotFoundException("Sidebar group not found.");
    }

    public async Task AssignRoomAsync(Guid groupId, Guid userId, Guid roomId, CancellationToken ct = default)
    {
        var groupExists = await _db.SidebarGroups
            .AnyAsync(g => g.Id == groupId && g.UserId == userId, ct);

        if (!groupExists)
            throw new KeyNotFoundException("Sidebar group not found.");

        var maxPosition = await _db.RoomMemberships
            .Where(m => m.UserId == userId && m.SidebarGroupId == groupId)
            .Select(m => (int?)m.Position)
            .MaxAsync(ct) ?? -1;

        var rows = await _db.RoomMemberships
            .Where(m => m.UserId == userId && m.RoomId == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.SidebarGroupId, groupId)
                .SetProperty(m => m.Position, maxPosition + 1), ct);

        if (rows == 0)
            throw new KeyNotFoundException("Room membership not found.");
    }

    public async Task UnassignRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default)
    {
        var maxPosition = await _db.RoomMemberships
            .Where(m => m.UserId == userId && m.SidebarGroupId == null)
            .Select(m => (int?)m.Position)
            .MaxAsync(ct) ?? -1;

        await _db.RoomMemberships
            .Where(m => m.UserId == userId && m.RoomId == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.SidebarGroupId, (Guid?)null)
                .SetProperty(m => m.Position, maxPosition + 1), ct);
    }

    public async Task ReorderAsync(Guid userId, Guid? groupId, IReadOnlyList<Guid> roomIds, CancellationToken ct = default)
    {
        for (var i = 0; i < roomIds.Count; i++)
        {
            var roomId = roomIds[i];
            var position = i;
            await _db.RoomMemberships
                .Where(m => m.UserId == userId && m.RoomId == roomId && m.SidebarGroupId == groupId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Position, position), ct);
        }
    }

    public async Task SetCollapsedAsync(Guid groupId, Guid userId, bool isCollapsed, CancellationToken ct = default)
    {
        var rows = await _db.SidebarGroups
            .Where(g => g.Id == groupId && g.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsCollapsed, isCollapsed), ct);

        if (rows == 0)
            throw new KeyNotFoundException("Sidebar group not found.");
    }
}
