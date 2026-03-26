namespace Shared.Contracts.DTOs;

public record LinkPreviewDataDto(
    string Url,
    string? Title,
    string? Description,
    string? ThumbnailUrl,
    bool IsDismissed
);
