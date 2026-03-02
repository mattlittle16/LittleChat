namespace Shared.Contracts.DTOs;

public record UserDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl
);
