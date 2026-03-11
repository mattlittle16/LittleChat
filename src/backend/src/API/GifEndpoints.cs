using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API;

public static class GifEndpoints
{
    public static IEndpointRouteBuilder MapGifEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gif/search", [Authorize] async (
            [FromQuery] string? q,
            [FromQuery] int limit,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest("Query parameter 'q' is required.");

            if (q.Length > 50)
                q = q[..50];

            limit = limit <= 0 ? 20 : Math.Min(limit, 50);

            var apiKey = configuration["Klipy:ApiKey"]
                ?? configuration["KLIPY_API_KEY"]
                ?? string.Empty;

            var client = httpClientFactory.CreateClient("Klipy");

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(
                    $"search?q={Uri.EscapeDataString(q)}&key={Uri.EscapeDataString(apiKey)}&limit={limit}",
                    cancellationToken);
            }
            catch (Exception)
            {
                return Results.StatusCode(502);
            }

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429)
                    return Results.StatusCode(503);
                return Results.StatusCode(502);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            KlipySearchResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<KlipySearchResponse>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return Results.StatusCode(502);
            }

            if (parsed?.Results is null)
                return Results.Ok(Array.Empty<GifSearchResultDto>());

            var results = parsed.Results
                .Where(r => r.MediaFormats?.Gif?.Url is not null)
                .Select(r => new GifSearchResultDto(
                    r.Id ?? string.Empty,
                    r.ContentDescription ?? string.Empty,
                    r.MediaFormats?.Tinygif?.Url ?? r.MediaFormats!.Gif!.Url!,
                    r.MediaFormats!.Gif!.Url!))
                .ToArray();

            return Results.Ok(results);
        });

        return app;
    }
}

record GifSearchResultDto(string Id, string Title, string PreviewUrl, string GifUrl);

// Klipy Tenor-compatible v2 response shape
file class KlipySearchResponse
{
    [JsonPropertyName("results")]
    public List<KlipyResult>? Results { get; set; }
}

file class KlipyResult
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("content_description")]
    public string? ContentDescription { get; set; }

    [JsonPropertyName("media_formats")]
    public KlipyMediaFormats? MediaFormats { get; set; }
}

file class KlipyMediaFormats
{
    [JsonPropertyName("gif")]
    public KlipyMediaUrl? Gif { get; set; }

    [JsonPropertyName("tinygif")]
    public KlipyMediaUrl? Tinygif { get; set; }
}

file class KlipyMediaUrl
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
