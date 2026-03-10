using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
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
                var contentType = reader.GetString(2);
                var roomId      = reader.GetGuid(3);
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

        return app;
    }
}
