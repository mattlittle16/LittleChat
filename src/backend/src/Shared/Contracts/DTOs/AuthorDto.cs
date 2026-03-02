namespace Shared.Contracts.DTOs;

public record AuthorDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl
);
