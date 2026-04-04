using Files.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts;

namespace Files.API;

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
                IAttachmentRepository repo,
                IDataProtectionProvider dataProtection,
                CancellationToken cancellationToken) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var attachment = await repo.GetAttachmentWithRoomAsync(attachmentId, cancellationToken);
                if (attachment is null)
                    return Results.NotFound();

                // Only issue tokens for video attachments
                if (!attachment.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                    return Results.NotFound();

                // Validate room membership
                var isMember = await repo.IsUserRoomMemberAsync(attachment.RoomId, userId.Value, cancellationToken);
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
