using MediatR;
using Messaging.Application.Commands;
using Messaging.Application.Options;
using Messaging.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Contracts.DTOs;

namespace Messaging.API;

public static class MessagingEndpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/rooms — rooms for the current user
        app.MapGet("/api/rooms",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var rooms = await sender.Send(new GetRoomsQuery(userId.Value), ctx.RequestAborted);
                return Results.Ok(rooms);
            });

        // POST /api/rooms — create a new room (auto-adds all users)
        app.MapPost("/api/rooms",
            [Authorize] async (CreateRoomBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(body.Name) || body.Name.Length > 100)
                    return Results.BadRequest("Room name must be between 1 and 100 characters.");

                try
                {
                    var room = await sender.Send(new CreateRoomCommand(body.Name, userId.Value), ctx.RequestAborted);
                    return Results.Created($"/api/rooms/{room.Id}", new RoomDto(
                        room.Id, room.Name, room.IsDm,
                        UnreadCount: 0, HasMention: false, LastMessagePreview: null,
                        room.CreatedAt));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // GET /api/rooms/{roomId}/messages — paginated message history
        app.MapGet("/api/rooms/{roomId:guid}/messages",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender,
                IOptions<MessagingOptions> opts,
                DateTime? before, Guid? beforeId, int? limit) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var pageSize = Math.Clamp(limit ?? opts.Value.MessagePageSize, 1, 100);

                try
                {
                    var page = await sender.Send(
                        new GetMessagesQuery(roomId, userId.Value, before, beforeId, pageSize),
                        ctx.RequestAborted);

                    var dtos = page.Messages.Select(m => new MessageDto(
                        Id: m.Id,
                        RoomId: m.RoomId,
                        Author: new AuthorDto(m.UserId, m.AuthorDisplayName, m.AuthorAvatarUrl),
                        Content: m.Content,
                        Attachments: m.Attachments
                            .Select(a => new AttachmentDto(
                                a.Id, a.FileName, a.FileSize, a.ContentType, a.IsImage,
                                $"/api/files/attachments/{a.Id}"))
                            .ToList(),
                        Reactions: m.Reactions
                            .GroupBy(r => r.Emoji)
                            .Select(g => new ReactionDto(g.Key, g.Count(), g.Select(r => r.UserDisplayName).ToList()))
                            .ToList(),
                        CreatedAt: m.CreatedAt,
                        EditedAt: m.EditedAt
                    )).ToList();

                    return Results.Ok(new { messages = dtos, hasMore = page.HasMore });
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        // POST /api/rooms/dm — get or create a DM conversation
        app.MapPost("/api/rooms/dm",
            [Authorize] async (DmBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    var (room, isNew) = await sender.Send(
                        new CreateOrGetDmCommand(userId.Value, body.TargetUserId), ctx.RequestAborted);

                    var dto = new RoomDto(
                        room.Id, Name: string.Empty, IsDm: true,
                        UnreadCount: 0, HasMention: false, LastMessagePreview: null,
                        room.CreatedAt == default ? DateTime.UtcNow : room.CreatedAt);

                    return isNew ? Results.Created($"/api/rooms/{room.Id}", dto) : Results.Ok(dto);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // POST /api/rooms/{roomId}/messages — REST / file upload path (multipart/form-data)
        app.MapPost("/api/rooms/{roomId:guid}/messages",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender) =>
            {
                // Raise per-request body size limit to 500 MB (combined cap)
                const long maxCombinedBytes = 500L * 1024 * 1024;
                const long maxPerFileBytes  = 200L * 1024 * 1024;
                const int  maxFileCount     = 15;

                var bodySizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (bodySizeFeature is not null && !bodySizeFeature.IsReadOnly)
                    bodySizeFeature.MaxRequestBodySize = maxCombinedBytes + (1024 * 1024);

                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var displayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";
                var avatarUrl   = ctx.User.FindFirst("picture")?.Value;

                string? content;
                Guid?   messageId;
                IReadOnlyList<IFormFile> formFiles = [];

                if (ctx.Request.HasFormContentType)
                {
                    var form  = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
                    content   = form["content"].FirstOrDefault();
                    messageId = Guid.TryParse(form["id"].FirstOrDefault(), out var fid) ? fid : (Guid?)null;
                    formFiles = form.Files.GetFiles("file").ToList();
                }
                else
                {
                    // JSON body fallback (text-only, no files)
                    var body  = await ctx.Request.ReadFromJsonAsync<SendMessageBody>(ctx.RequestAborted);
                    content   = body?.Content;
                    messageId = body?.MessageId;
                }

                content ??= string.Empty;

                // Validate: must have content OR files
                if (string.IsNullOrWhiteSpace(content) && formFiles.Count == 0)
                    return Results.BadRequest("Message must have text content or at least one file attachment.");

                // Validate file count
                if (formFiles.Count > maxFileCount)
                    return Results.BadRequest($"A message may have at most {maxFileCount} file attachments.");

                // Dangerous extension blocklist
                var blockedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".exe", ".msi", ".dmg", ".pkg", ".app", ".deb", ".rpm",
                    ".sh", ".bash", ".zsh", ".ps1", ".psm1", ".psd1",
                    ".bat", ".cmd", ".com", ".vbs", ".vbe", ".js", ".jse",
                    ".wsf", ".wsh", ".scr", ".pif", ".cpl", ".dll", ".sys",
                    ".drv", ".jar", ".py", ".rb", ".pl", ".php"
                };

                var blockedFiles = formFiles
                    .Where(f => blockedExtensions.Contains(Path.GetExtension(f.FileName)))
                    .Select(f => f.FileName)
                    .ToList();

                if (blockedFiles.Count > 0)
                    return Results.BadRequest(new { error = "One or more file types are not permitted.", blockedFiles });

                // Validate per-file and combined sizes
                foreach (var f in formFiles)
                {
                    if (f.Length > maxPerFileBytes)
                        return Results.BadRequest($"File '{f.FileName}' exceeds the 200 MB per-file limit.");
                }

                if (formFiles.Sum(f => f.Length) > maxCombinedBytes)
                    return Results.BadRequest("Combined file size exceeds the 500 MB limit per message.");

                try
                {
                    var fileUploads = formFiles
                        .Select(f => new Messaging.Application.Commands.FileUpload(f.OpenReadStream(), f.FileName, f.Length))
                        .ToList();

                    var result = await sender.Send(new SendMessageCommand(
                        MessageId:         messageId ?? Guid.NewGuid(),
                        RoomId:            roomId,
                        UserId:            userId.Value,
                        AuthorDisplayName: displayName,
                        AuthorAvatarUrl:   avatarUrl,
                        Content:           content,
                        Files:             fileUploads
                    ), ctx.RequestAborted);

                    return Results.Ok(new { id = result.MessageId, failedFiles = result.FailedFileNames });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            }).DisableRequestTimeout()
              .WithMetadata(new DisableRequestSizeLimitAttribute());

        // PATCH /api/rooms/{roomId}/messages/{messageId} — edit own message
        app.MapPatch("/api/rooms/{roomId:guid}/messages/{messageId:guid}",
            [Authorize] async (Guid roomId, Guid messageId, EditMessageBody body,
                HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new EditMessageCommand(messageId, roomId, userId.Value, body.Content),
                        ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // DELETE /api/rooms/{roomId} — permanently delete a room (DM or group)
        app.MapDelete("/api/rooms/{roomId:guid}",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender, Messaging.Domain.IRoomRepository rooms) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var room = await rooms.GetByIdAsync(roomId, ctx.RequestAborted);
                if (room is null)
                    return Results.NotFound();

                try
                {
                    if (room.IsDm)
                        await sender.Send(new DeleteDmCommand(roomId, userId.Value), ctx.RequestAborted);
                    else
                        await sender.Send(new DeleteRoomCommand(roomId, userId.Value), ctx.RequestAborted);

                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // DELETE /api/rooms/{roomId}/messages/{messageId} — hard delete own message
        app.MapDelete("/api/rooms/{roomId:guid}/messages/{messageId:guid}",
            [Authorize] async (Guid roomId, Guid messageId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new DeleteMessageCommand(messageId, roomId, userId.Value),
                        ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // POST /api/rooms/{roomId}/read — mark conversation as fully read (US3)
        app.MapPost("/api/rooms/{roomId:guid}/read",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender, Messaging.Domain.IRoomRepository rooms) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var room = await rooms.GetByIdAsync(roomId, ctx.RequestAborted);
                if (room is null)
                    return Results.NotFound();

                var isMember = await sender.Send(new MarkRoomReadCommand(roomId, userId.Value), ctx.RequestAborted);
                return isMember ? Results.NoContent() : Results.Forbid();
            });

        return app;
    }
}

internal sealed record SendMessageBody(Guid? MessageId, string Content);
internal sealed record EditMessageBody(string Content);
internal sealed record CreateRoomBody(string Name);
internal sealed record DmBody(Guid TargetUserId);
