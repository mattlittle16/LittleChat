namespace Shared.Contracts.DTOs;

public record UserDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    string? ProfileImageUrl,
    string OnboardingStatus
);
