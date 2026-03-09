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
        // GET /api/files/{messageId} — serve attachment, authenticated + membership check
        app.MapGet("/api/files/{messageId:guid}",
            [Authorize] async (Guid messageId, HttpContext ctx, NpgsqlDataSource db, IConfiguration config) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                // Fetch message metadata
                await using var cmd = db.CreateCommand(
                    """
                    SELECT m.file_path, m.file_name, m.room_id
                    FROM messages m
                    WHERE m.id = $1
                    """);
                cmd.Parameters.AddWithValue(messageId);

                await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
                if (!await reader.ReadAsync(ctx.RequestAborted))
                    return Results.NotFound();

                var filePath = reader.IsDBNull(0) ? null : reader.GetString(0);
                var fileName = reader.IsDBNull(1) ? null : reader.GetString(1);
                var roomId   = reader.GetGuid(2);
                await reader.CloseAsync();

                if (filePath is null || fileName is null)
                    return Results.NotFound();

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

                // Detect MIME type
                var provider = new FileExtensionContentTypeProvider();
                provider.Mappings[".heic"] = "image/heic";
                provider.Mappings[".heif"] = "image/heif";
                if (!provider.TryGetContentType(fileName, out var contentType))
                    contentType = "application/octet-stream";

                var stream = File.OpenRead(fullPath);
                return Results.File(stream, contentType,
                    fileDownloadName: fileName,
                    enableRangeProcessing: true);
            });

        return app;
    }
}
