using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shared.Contracts;

namespace Files.API;

public static class FilesEndpoints
{
    public static IEndpointRouteBuilder MapFilesEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/files/attachments/{attachmentId} — serve attachment, authenticated + membership check
        app.MapGet("/api/files/attachments/{attachmentId:guid}",
            [Authorize] async (Guid attachmentId, HttpContext ctx, NpgsqlDataSource db, IConfiguration config) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                // Fetch attachment metadata + room via join
                await using var cmd = db.CreateCommand(
                    """
                    SELECT a.file_path, a.file_name, a.content_type, m.room_id
                    FROM message_attachments a
                    JOIN messages m ON m.id = a.message_id
                    WHERE a.id = $1
                    """);
                cmd.Parameters.AddWithValue(attachmentId);

                await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
                if (!await reader.ReadAsync(ctx.RequestAborted))
                    return Results.NotFound();

                var filePath    = reader.GetString(0);
                var fileName    = reader.GetString(1);
                var rawType     = reader.GetString(2);
                var roomId      = reader.GetGuid(3);

                // Sanitize content-type before using in response header; fall back to safe default
                var contentType = (rawType.Length <= 100
                    && !rawType.Contains('\n') && !rawType.Contains('\r')
                    && System.Text.RegularExpressions.Regex.IsMatch(rawType, @"^[\w\-]+/[\w\-\+\.]+$"))
                    ? rawType : "application/octet-stream";
                await reader.CloseAsync();

                // Check membership
                await using var memberCmd = db.CreateCommand(
                    "SELECT 1 FROM room_memberships WHERE room_id = $1 AND user_id = $2");
                memberCmd.Parameters.AddWithValue(roomId);
                memberCmd.Parameters.AddWithValue(userId.Value);
                var isMember = await memberCmd.ExecuteScalarAsync(ctx.RequestAborted) is not null;
                if (!isMember)
                    return Results.Forbid();

                var uploadPath = config["UPLOAD_PATH"] ?? "/uploads";
                var fullPath = Path.GetFullPath(Path.Combine(uploadPath, filePath));
                if (!fullPath.StartsWith(Path.GetFullPath(uploadPath) + Path.DirectorySeparatorChar))
                    return Results.NotFound();
                if (!File.Exists(fullPath))
                    return Results.NotFound();

                var stream = File.OpenRead(fullPath);
                return Results.File(stream, contentType,
                    fileDownloadName: fileName,
                    enableRangeProcessing: true);
            });

        // GET /api/files/video/{token} [AllowAnonymous]
        // Streams an MP4 using a signed token issued by VideoTokenEndpoints.
        // The token encodes the attachmentId and expires after 1 hour.
        app.MapGet("/api/files/video/{token}",
            [AllowAnonymous] async (
                string token,
                HttpContext ctx,
                NpgsqlDataSource db,
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

                await using var cmd = db.CreateCommand(
                    """
                    SELECT a.file_path, a.file_name, a.content_type
                    FROM message_attachments a
                    WHERE a.id = $1
                    """);
                cmd.Parameters.AddWithValue(attachmentId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return Results.NotFound();

                var filePath = reader.GetString(0);
                var fileName = reader.GetString(1);
                var contentType = reader.GetString(2);
                await reader.CloseAsync();

                var uploadPath = config["UPLOAD_PATH"] ?? "/uploads";
                var fullPath = Path.GetFullPath(Path.Combine(uploadPath, filePath));
                if (!fullPath.StartsWith(Path.GetFullPath(uploadPath) + Path.DirectorySeparatorChar))
                    return Results.NotFound();
                if (!File.Exists(fullPath))
                    return Results.NotFound();

                var stream = File.OpenRead(fullPath);
                return Results.File(stream, contentType,
                    fileDownloadName: fileName,
                    enableRangeProcessing: true);
            });

        return app;
    }
}
