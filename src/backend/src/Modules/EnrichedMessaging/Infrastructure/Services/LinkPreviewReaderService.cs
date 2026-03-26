using Microsoft.Extensions.Configuration;
using Npgsql;
using Shared.Contracts.DTOs;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Infrastructure.Services;

public sealed class LinkPreviewReaderService : ILinkPreviewReader
{
    private readonly string _connectionString;

    public LinkPreviewReaderService(IConfiguration configuration)
    {
        _connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
    }

    public async Task<IReadOnlyDictionary<Guid, LinkPreviewDataDto>> GetForMessagesAsync(
        IReadOnlyList<Guid> messageIds, CancellationToken ct = default)
    {
        if (messageIds.Count == 0)
            return new Dictionary<Guid, LinkPreviewDataDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = new NpgsqlCommand(
            "SELECT message_id, url, title, description, thumbnail_url, is_dismissed " +
            "FROM link_previews WHERE message_id = ANY(@ids)", conn);
        cmd.Parameters.AddWithValue("ids", messageIds.ToArray());

        var result = new Dictionary<Guid, LinkPreviewDataDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var msgId = reader.GetGuid(0);
            result[msgId] = new LinkPreviewDataDto(
                Url:          reader.GetString(1),
                Title:        reader.IsDBNull(2) ? null : reader.GetString(2),
                Description:  reader.IsDBNull(3) ? null : reader.GetString(3),
                ThumbnailUrl: reader.IsDBNull(4) ? null : reader.GetString(4),
                IsDismissed:  reader.GetBoolean(5)
            );
        }
        return result;
    }
}
