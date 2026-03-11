using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Shared.Contracts;

namespace API;

public static class VideoTokenEndpoints
{
    public static IEndpointRouteBuilder MapVideoTokenEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/files/video-token/{attachmentId}
        // Issues a short-lived signed URL for streaming an MP4 attachment.
        // The browser's <video> element cannot send Authorization headers, so we
        // issue a 1-hour cryptographically signed token instead.
        app.MapGet("/api/files/video-token/{attachmentId:guid}",
            [Authorize] async (
                Guid attachmentId,
                HttpContext ctx,
                NpgsqlDataSource db,
                IDataProtectionProvider dataProtection,
                CancellationToken cancellationToken) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                // Fetch attachment metadata + room
                await using var cmd = db.CreateCommand(
                    """
                    SELECT a.content_type, m.room_id
                    FROM message_attachments a
                    JOIN messages m ON m.id = a.message_id
                    WHERE a.id = $1
                    """);
                cmd.Parameters.AddWithValue(attachmentId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return Results.NotFound();

                var contentType = reader.GetString(0);
                var roomId = reader.GetGuid(1);
                await reader.CloseAsync();

                // Only issue tokens for video attachments
                if (!contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                    return Results.NotFound();

                // Validate room membership
                await using var memberCmd = db.CreateCommand(
                    "SELECT 1 FROM room_memberships WHERE room_id = $1 AND user_id = $2");
                memberCmd.Parameters.AddWithValue(roomId);
                memberCmd.Parameters.AddWithValue(userId.Value);
                var isMember = await memberCmd.ExecuteScalarAsync(cancellationToken) is not null;
                if (!isMember)
                    return Results.Forbid();

                // Issue a time-limited signed token encoding the attachmentId
                var protector = dataProtection
                    .CreateProtector("VideoToken")
                    .ToTimeLimitedDataProtector();

                var token = protector.Protect(attachmentId.ToString(), TimeSpan.FromHours(1));

                return Results.Ok(new { videoUrl = $"/api/files/video/{Uri.EscapeDataString(token)}" });
            });

        return app;
    }
}
