using System.Text.RegularExpressions;
using EnrichedMessaging.Application.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace EnrichedMessaging.Infrastructure.Services;

public sealed class LinkPreviewFetcherService : ILinkPreviewFetcher
{
    private readonly HttpClient _http;
    private readonly ILogger<LinkPreviewFetcherService> _logger;

    public LinkPreviewFetcherService(HttpClient http, ILogger<LinkPreviewFetcherService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<LinkPreviewResult?> FetchAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return null;

            var html = await response.Content.ReadAsStringAsync(ct);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = GetMetaContent(doc, "og:title")
                ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
            var description = GetMetaContent(doc, "og:description");
            var image = GetMetaContent(doc, "og:image");

            if (title is null && description is null && image is null)
                return null;

            return new LinkPreviewResult(url, title, description, image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Link preview fetch failed for {Url}", url);
            return null;
        }
    }

    private static string? GetMetaContent(HtmlDocument doc, string property)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//meta[@property='{property}']") ??
            doc.DocumentNode.SelectSingleNode(
            $"//meta[@name='{property}']");
        return node?.GetAttributeValue("content", null);
    }
}
