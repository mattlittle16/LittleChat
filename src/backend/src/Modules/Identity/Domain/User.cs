namespace Identity.Domain;

public sealed record User(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    DateTime CreatedAt
);
