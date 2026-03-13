namespace Identity.Domain;

public sealed record User(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    string? ProfileImagePath,
    float? CropX,
    float? CropY,
    float? CropZoom,
    DateTime CreatedAt
);
