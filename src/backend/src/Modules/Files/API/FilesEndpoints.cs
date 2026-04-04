using Files.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Shared.Contracts;

namespace Files.API;

public static class FilesEndpoints
{
    public static IEndpointRouteBuilder MapFilesEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/files/attachments/{attachmentId} — serve attachment, authenticated + membership check
        app.MapGet("/api/files/attachments/{attachmentId:guid}",
            [Authorize] async (Guid attachmentId, HttpContext ctx, IAttachmentRepository repo, IConfiguration config) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var attachment = await repo.GetAttachmentWithRoomAsync(attachmentId, ctx.RequestAborted);
                if (attachment is null)
                    return Results.NotFound();

                // Sanitize content-type before using in response header; fall back to safe default
                var contentType = (attachment.ContentType.Length <= 100
                    && !attachment.ContentType.Contains('\n') && !attachment.ContentType.Contains('\r')
                    && System.Text.RegularExpressions.Regex.IsMatch(attachment.ContentType, @"^[\w\-]+/[\w\-\+\.]+$"))
                    ? attachment.ContentType : "application/octet-stream";

                // Check membership
                var isMember = await repo.IsUserRoomMemberAsync(attachment.RoomId, userId.Value, ctx.RequestAborted);
                if (!isMember)
                    return Results.Forbid();

                var uploadPath = config["UPLOAD_PATH"] ?? "/uploads";
                var fullPath = Path.GetFullPath(Path.Combine(uploadPath, attachment.FilePath));
                if (!fullPath.StartsWith(Path.GetFullPath(uploadPath) + Path.DirectorySeparatorChar))
                    return Results.NotFound();
                if (!File.Exists(fullPath))
                    return Results.NotFound();

                var stream = File.OpenRead(fullPath);
                return Results.File(stream, contentType,
                    fileDownloadName: attachment.FileName,
                    enableRangeProcessing: true);
            });

        // GET /api/files/video/{token} [AllowAnonymous]
        // Streams an MP4 using a signed token issued by VideoTokenEndpoints.
        // The token encodes the attachmentId and expires after 1 hour.
        app.MapGet("/api/files/video/{token}",
            [AllowAnonymous] async (
                string token,
                HttpContext ctx,
                IAttachmentRepository repo,
                IDataProtectionProvider dataProtection,
                IConfiguration config,
                CancellationToken cancellationToken) =>
            {
                Guid attachmentId;
                try
                {
                    var protector = dataProtection
                        .CreateProtector("VideoToken")
                        .ToTimeLimitedDataProtector();

                    var decoded = protector.Unprotect(token);
                    attachmentId = Guid.Parse(decoded);
                }
                catch
                {
                    return Results.Unauthorized();
                }

                var attachment = await repo.GetAttachmentAsync(attachmentId, cancellationToken);
                if (attachment is null)
                    return Results.NotFound();

                var uploadPath = config["UPLOAD_PATH"] ?? "/uploads";
                var fullPath = Path.GetFullPath(Path.Combine(uploadPath, attachment.FilePath));
                if (!fullPath.StartsWith(Path.GetFullPath(uploadPath) + Path.DirectorySeparatorChar))
                    return Results.NotFound();
                if (!File.Exists(fullPath))
                    return Results.NotFound();

                var stream = File.OpenRead(fullPath);
                return Results.File(stream, attachment.ContentType,
                    fileDownloadName: attachment.FileName,
                    enableRangeProcessing: true);
            });

        app.MapVideoTokenEndpoints();

        return app;
    }
}
