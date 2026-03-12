namespace Shared.Contracts.DTOs;

public record RoomMemberDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    bool IsOwner
);
