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

        return app;
    }
}

internal sealed record SendMessageBody(Guid? MessageId, string Content);
internal sealed record CreateRoomBody(string Name);
