namespace Shared.Contracts.DTOs;

public record SidebarGroupDto(
    Guid Id,
    string Name,
    int DisplayOrder,
    bool IsCollapsed,
    IReadOnlyList<Guid> RoomIds
);
