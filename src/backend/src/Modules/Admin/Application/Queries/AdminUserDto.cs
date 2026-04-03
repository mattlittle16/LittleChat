namespace LittleChat.Modules.Admin.Application.Queries;

public sealed record AdminUserDto(Guid Id, string DisplayName, string? AvatarUrl, string? ProfileImageUrl = null, DateTimeOffset? BannedUntil = null);
