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

    private const int MaxResponseBytes = 5 * 1024 * 1024; // 5 MB

    public async Task<LinkPreviewResult?> FetchAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return null;

            // Reject based on declared Content-Length before reading the body
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var limited = new System.IO.StreamReader(
                new LimitedStream(stream, MaxResponseBytes));
            var html = await limited.ReadToEndAsync(ct);

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

/// <summary>
/// Wraps a stream and throws if more than <paramref name="maxBytes"/> are read,
/// preventing unbounded memory consumption on large or malicious responses.
/// </summary>
internal sealed class LimitedStream(Stream inner, long maxBytes) : Stream
{
    private long _bytesRead;

    public override bool CanRead  => true;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        _bytesRead += n;
        if (_bytesRead > maxBytes)
            throw new InvalidOperationException("Response exceeded maximum allowed size.");
        return n;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        var n = await inner.ReadAsync(buffer, offset, count, ct);
        _bytesRead += n;
        if (_bytesRead > maxBytes)
            throw new InvalidOperationException("Response exceeded maximum allowed size.");
        return n;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await inner.ReadAsync(buffer, ct);
        _bytesRead += n;
        if (_bytesRead > maxBytes)
            throw new InvalidOperationException("Response exceeded maximum allowed size.");
        return n;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value)                => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
