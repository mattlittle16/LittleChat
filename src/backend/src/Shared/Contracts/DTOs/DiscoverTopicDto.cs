namespace Shared.Contracts.DTOs;

public record DiscoverTopicDto(
    Guid Id,
    string Name,
    int MemberCount,
    DateTime CreatedAt
);
