namespace Shared.Contracts.DTOs;

public sealed record SearchResultDto(
    Guid MessageId,
    Guid RoomId,
    string RoomName,
    string AuthorDisplayName,
    string Content,
    DateTime CreatedAt);
