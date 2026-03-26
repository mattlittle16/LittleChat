namespace EnrichedMessaging.Application.Services;

public sealed record LinkPreviewResult(
    string Url,
    string? Title,
    string? Description,
    string? ThumbnailUrl
);

public interface ILinkPreviewFetcher
{
    Task<LinkPreviewResult?> FetchAsync(string url, CancellationToken ct = default);
}
