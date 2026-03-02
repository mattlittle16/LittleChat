using MediatR;
using Messaging.Application.Commands;
using Messaging.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                var rooms = await sender.Send(new GetRoomsQuery(userId), ctx.RequestAborted);
                return Results.Ok(rooms);
            });

        // POST /api/rooms — create a new room (auto-adds all users)
        app.MapPost("/api/rooms",
            [Authorize] async (CreateRoomBody body, HttpContext ctx, ISender sender) =>
            {
                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                try
                {
                    var room = await sender.Send(new CreateRoomCommand(body.Name, userId), ctx.RequestAborted);
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
                DateTime? before, Guid? beforeId, int limit = 50) =>
            {
                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                try
                {
                    var page = await sender.Send(
                        new GetMessagesQuery(roomId, userId, before, beforeId, Math.Clamp(limit, 1, 100)),
                        ctx.RequestAborted);

                    var dtos = page.Messages.Select(m => new MessageDto(
                        Id: m.Id,
                        RoomId: m.RoomId,
                        Author: new AuthorDto(m.UserId, m.AuthorDisplayName, m.AuthorAvatarUrl),
                        Content: m.Content,
                        Attachment: m.FileName is not null && m.FileSize is not null
                            ? new AttachmentDto(m.FileName, m.FileSize.Value, $"/api/files/{m.Id}")
                            : null,
                        Reactions: [],
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
                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                try
                {
                    var (room, isNew) = await sender.Send(
                        new CreateOrGetDmCommand(userId, body.TargetUserId), ctx.RequestAborted);

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

        // POST /api/rooms/{roomId}/messages — REST fallback for message send
        app.MapPost("/api/rooms/{roomId:guid}/messages",
            [Authorize] async (Guid roomId, SendMessageBody body, HttpContext ctx, ISender sender) =>
            {
                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                var displayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";
                var avatarUrl = ctx.User.FindFirst("picture")?.Value;

                try
                {
                    var id = await sender.Send(new SendMessageCommand(
                        MessageId: body.MessageId ?? Guid.NewGuid(),
                        RoomId: roomId,
                        UserId: userId,
                        AuthorDisplayName: displayName,
                        AuthorAvatarUrl: avatarUrl,
                        Content: body.Content
                    ), ctx.RequestAborted);

                    return Results.Ok(new { id });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        // PATCH /api/rooms/{roomId}/messages/{messageId} — edit own message
        app.MapPatch("/api/rooms/{roomId:guid}/messages/{messageId:guid}",
            [Authorize] async (Guid roomId, Guid messageId, EditMessageBody body,
                HttpContext ctx, ISender sender) =>
            {
                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new EditMessageCommand(messageId, roomId, userId, body.Content),
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

        // DELETE /api/rooms/{roomId}/messages/{messageId} — hard delete own message
        app.MapDelete("/api/rooms/{roomId:guid}/messages/{messageId:guid}",
            [Authorize] async (Guid roomId, Guid messageId, HttpContext ctx, ISender sender) =>
            {
                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new DeleteMessageCommand(messageId, roomId, userId),
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

        return app;
    }
}

internal sealed record SendMessageBody(Guid? MessageId, string Content);
internal sealed record EditMessageBody(string Content);
internal sealed record CreateRoomBody(string Name);
internal sealed record DmBody(Guid TargetUserId);
