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
                    var room = await sender.Send(
                        new CreateRoomCommand(body.Name, userId.Value, body.IsPrivate, body.InvitedUserIds),
                        ctx.RequestAborted);
                    var memberCount = 1 + (body.InvitedUserIds?.Count ?? 0);
                    return Results.Created($"/api/rooms/{room.Id}", new RoomDto(
                        room.Id, room.Name, room.IsDm,
                        UnreadCount: 0, HasMention: false, LastMessagePreview: null,
                        room.CreatedAt,
                        IsPrivate: room.IsPrivate, OwnerId: room.OwnerId,
                        IsProtected: room.IsProtected, MemberCount: memberCount));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // GET /api/rooms/{roomId}/messages — paginated message history
        // Modes: default (before/beforeId), aroundId, after/afterId
        app.MapGet("/api/rooms/{roomId:guid}/messages",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender,
                IOptions<MessagingOptions> opts,
                DateTime? before, Guid? beforeId, Guid? aroundId,
                DateTime? after, Guid? afterId, int? limit) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var pageSize = Math.Clamp(limit ?? opts.Value.MessagePageSize, 1, 100);

                try
                {
                    var page = await sender.Send(
                        new GetMessagesQuery(roomId, userId.Value, before, beforeId, pageSize,
                            AroundId: aroundId, After: after, AfterId: afterId),
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
                        EditedAt: m.EditedAt,
                        IsSystem: m.IsSystem
                    )).ToList();

                    return Results.Ok(new { messages = dtos, hasMore = page.HasMore, hasNewer = page.HasNewer });
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

                // Safe extension allowlist — explicit opt-in is more secure than a blocklist
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Images
                    ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif",
                    ".heic", ".heif",
                    // Documents
                    ".pdf", ".txt", ".md", ".csv", ".rtf",
                    // Office
                    ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt", ".odt", ".ods", ".odp",
                    // Archives
                    ".zip", ".7z", ".tar", ".gz", ".rar",
                    // Audio
                    ".mp3", ".m4a", ".wav", ".ogg", ".aac", ".flac",
                    // Video
                    ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v", ".wmv", ".flv",
                };

                var blockedFiles = formFiles
                    .Where(f => !allowedExtensions.Contains(Path.GetExtension(f.FileName)))
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
                        .Select(f => new Messaging.Application.Commands.FileUpload(
                            new LimitedStream(f.OpenReadStream(), maxPerFileBytes), f.FileName, f.Length))
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

        // GET /api/rooms/{roomId}/members — list members (must be a member)
        app.MapGet("/api/rooms/{roomId:guid}/members",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                try
                {
                    var members = await sender.Send(new GetRoomMembersQuery(roomId, userId.Value), ctx.RequestAborted);
                    return Results.Ok(members);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // POST /api/rooms/{roomId}/invite — invite a user (any member can invite)
        app.MapPost("/api/rooms/{roomId:guid}/invite",
            [Authorize] async (Guid roomId, InviteBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                var inviterDisplayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Someone";

                try
                {
                    await sender.Send(new InviteToRoomCommand(roomId, userId.Value, inviterDisplayName, body.TargetUserId), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // DELETE /api/rooms/{roomId}/members/{targetUserId} — remove member (owner only)
        app.MapDelete("/api/rooms/{roomId:guid}/members/{targetUserId:guid}",
            [Authorize] async (Guid roomId, Guid targetUserId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                var ownerDisplayName = ctx.User.FindFirst("preferred_username")?.Value ?? "The owner";

                try
                {
                    await sender.Send(new RemoveMemberCommand(roomId, userId.Value, ownerDisplayName, targetUserId), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // POST /api/rooms/{roomId}/transfer-ownership — transfer ownership (owner only)
        app.MapPost("/api/rooms/{roomId:guid}/transfer-ownership",
            [Authorize] async (Guid roomId, TransferOwnershipBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                var currentDisplayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";

                try
                {
                    await sender.Send(new TransferOwnershipCommand(
                        roomId, userId.Value, currentDisplayName,
                        body.NewOwnerUserId, body.NewOwnerDisplayName), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // POST /api/rooms/{roomId}/leave — leave a topic
        app.MapPost("/api/rooms/{roomId:guid}/leave",
            [Authorize] async (Guid roomId, LeaveRoomBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                var displayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";

                try
                {
                    await sender.Send(new LeaveRoomCommand(
                        roomId, userId.Value, displayName,
                        body.NewOwnerUserId, body.NewOwnerDisplayName), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // GET /api/rooms/discover — discover public topics not already joined
        app.MapGet("/api/rooms/discover",
            [Authorize] async (HttpContext ctx, ISender sender, string? q) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                var results = await sender.Send(new DiscoverTopicsQuery(userId.Value, q), ctx.RequestAborted);
                return Results.Ok(results);
            });

        // POST /api/rooms/{roomId}/join — join a public topic
        app.MapPost("/api/rooms/{roomId:guid}/join",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                try
                {
                    await sender.Send(new JoinRoomCommand(roomId, userId.Value), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // ── Sidebar Groups ─────────────────────────────────────────────────────────

        // GET /api/sidebar-groups — get all groups for the current user
        app.MapGet("/api/sidebar-groups",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                var groups = await sender.Send(new GetSidebarGroupsQuery(userId.Value), ctx.RequestAborted);
                return Results.Ok(groups);
            });

        // POST /api/sidebar-groups — create a new group
        app.MapPost("/api/sidebar-groups",
            [Authorize] async (SidebarGroupCreateBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                try
                {
                    var group = await sender.Send(new CreateSidebarGroupCommand(userId.Value, body.Name), ctx.RequestAborted);
                    return Results.Created($"/api/sidebar-groups/{group.Id}", group);
                }
                catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
            });

        // PATCH /api/sidebar-groups/{groupId} — rename
        app.MapPatch("/api/sidebar-groups/{groupId:guid}",
            [Authorize] async (Guid groupId, SidebarGroupCreateBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                try
                {
                    await sender.Send(new RenameSidebarGroupCommand(groupId, userId.Value, body.Name), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // DELETE /api/sidebar-groups/{groupId} — delete a group (rooms unassigned, not deleted)
        app.MapDelete("/api/sidebar-groups/{groupId:guid}",
            [Authorize] async (Guid groupId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                try
                {
                    await sender.Send(new DeleteSidebarGroupCommand(groupId, userId.Value), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // PUT /api/sidebar-groups/{groupId}/rooms/{roomId} — assign room to group
        app.MapPut("/api/sidebar-groups/{groupId:guid}/rooms/{roomId:guid}",
            [Authorize] async (Guid groupId, Guid roomId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                try
                {
                    await sender.Send(new AssignRoomToGroupCommand(groupId, userId.Value, roomId), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // DELETE /api/sidebar-groups/rooms/{roomId} — remove room from its group
        app.MapDelete("/api/sidebar-groups/rooms/{roomId:guid}",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                await sender.Send(new UnassignRoomFromGroupCommand(userId.Value, roomId), ctx.RequestAborted);
                return Results.NoContent();
            });

        // PATCH /api/sidebar-groups/rooms/reorder — reorder rooms within a bucket
        app.MapPatch("/api/sidebar-groups/rooms/reorder",
            [Authorize] async (ReorderRoomsBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                if (body.RoomIds is null || body.RoomIds.Count == 0)
                    return Results.BadRequest("RoomIds must not be empty.");

                try
                {
                    await sender.Send(new ReorderRoomMembershipsCommand(userId.Value, body.GroupId, body.RoomIds), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

        // PATCH /api/sidebar-groups/{groupId}/collapsed — set collapse state
        app.MapPatch("/api/sidebar-groups/{groupId:guid}/collapsed",
            [Authorize] async (Guid groupId, SidebarGroupCollapsedBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();

                try
                {
                    await sender.Send(new SetGroupCollapsedCommand(groupId, userId.Value, body.IsCollapsed), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException) { return Results.NotFound(); }
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

/// <summary>
/// Wraps a stream and throws if more than <c>maxBytes</c> are read through it,
/// enforcing the per-file size limit regardless of the client-reported Content-Length.
/// Fully proxies CanSeek/Seek so format decoders that require seekable streams (e.g. HEIC) work correctly.
/// </summary>
internal sealed class LimitedStream(Stream inner, long maxBytes) : Stream
{
    // Track the furthest byte position reached (high-water mark) so seeking back and
    // re-reading doesn't double-count bytes but a client can't escape the limit by seeking.
    private long _highWater;

    public override bool CanRead  => inner.CanRead;
    public override bool CanSeek  => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length   => inner.Length;
    public override long Position
    {
        get => inner.Position;
        set
        {
            inner.Position = value;
            _highWater = Math.Max(_highWater, value);
        }
    }

    public override void Flush() => inner.Flush();

    public override long Seek(long offset, SeekOrigin origin)
    {
        var pos = inner.Seek(offset, origin);
        _highWater = Math.Max(_highWater, pos);
        return pos;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        CheckHighWater();
        return n;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        var n = await inner.ReadAsync(buffer, offset, count, ct);
        CheckHighWater();
        return n;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await inner.ReadAsync(buffer, ct);
        CheckHighWater();
        return n;
    }

    private void CheckHighWater()
    {
        _highWater = Math.Max(_highWater, inner.CanSeek ? inner.Position : _highWater);
        if (_highWater > maxBytes)
            throw new InvalidOperationException($"File exceeds the {maxBytes / (1024 * 1024)} MB per-file size limit.");
    }

    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}

internal sealed record SendMessageBody(Guid? MessageId, string Content);
internal sealed record EditMessageBody(string Content);
internal sealed record CreateRoomBody(string Name, bool IsPrivate = false, IReadOnlyList<Guid>? InvitedUserIds = null);
internal sealed record DmBody(Guid TargetUserId);
internal sealed record InviteBody(Guid TargetUserId);
internal sealed record RemoveMemberBody(Guid TargetUserId);
internal sealed record TransferOwnershipBody(Guid NewOwnerUserId, string NewOwnerDisplayName);
internal sealed record LeaveRoomBody(Guid? NewOwnerUserId = null, string? NewOwnerDisplayName = null);
internal sealed record SidebarGroupCreateBody(string Name);
internal sealed record SidebarGroupCollapsedBody(bool IsCollapsed);
internal sealed record ReorderRoomsBody(Guid? GroupId, IReadOnlyList<Guid> RoomIds);
