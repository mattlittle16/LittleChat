using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.Commands;
using Notifications.Application.Queries;
using Shared.Contracts;

namespace Notifications.API;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/notifications/preferences
        app.MapGet("/api/notifications/preferences",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                var dto = await sender.Send(new GetPreferencesQuery(userId.Value), ctx.RequestAborted);
                return Results.Ok(dto);
            });

        // PUT /api/notifications/preferences
        app.MapPut("/api/notifications/preferences",
            [Authorize] async (UpsertPreferencesBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                try
                {
                    await sender.Send(new UpsertPreferencesCommand(
                        userId.Value,
                        body.DmSoundEnabled,
                        body.RoomSoundLevel,
                        body.DndEnabled,
                        body.BrowserNotificationsEnabled
                    ), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // GET /api/notifications/overrides
        app.MapGet("/api/notifications/overrides",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                var overrides = await sender.Send(new GetOverridesQuery(userId.Value), ctx.RequestAborted);
                return Results.Ok(overrides);
            });

        // PUT /api/notifications/overrides/{roomId}
        app.MapPut("/api/notifications/overrides/{roomId:guid}",
            [Authorize] async (Guid roomId, SetOverrideBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                try
                {
                    await sender.Send(new SetOverrideCommand(userId.Value, roomId, body.Level), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // DELETE /api/notifications/overrides/{roomId}
        app.MapDelete("/api/notifications/overrides/{roomId:guid}",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                await sender.Send(new DeleteOverrideCommand(userId.Value, roomId), ctx.RequestAborted);
                return Results.NoContent();
            });

        // GET /api/notifications — list recent notifications (unread + read, last 50, 30-day TTL)
        app.MapGet("/api/notifications",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                var notifications = await sender.Send(new GetNotificationsQuery(userId.Value), ctx.RequestAborted);
                return Results.Ok(notifications);
            });

        // POST /api/notifications/mark-read — mark specific notification IDs as read
        app.MapPost("/api/notifications/mark-read",
            [Authorize] async (MarkReadBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                await sender.Send(
                    new MarkNotificationsReadCommand(userId.Value, body.Ids, null, false),
                    ctx.RequestAborted);
                return Results.NoContent();
            });

        // POST /api/notifications/mark-read/room/{roomId} — mark all notifications for a room as read
        app.MapPost("/api/notifications/mark-read/room/{roomId:guid}",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                await sender.Send(
                    new MarkNotificationsReadCommand(userId.Value, null, roomId, false),
                    ctx.RequestAborted);
                return Results.NoContent();
            });

        // POST /api/notifications/mark-all-read — mark all notifications as read
        app.MapPost("/api/notifications/mark-all-read",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null) return Results.Unauthorized();
                await sender.Send(
                    new MarkNotificationsReadCommand(userId.Value, null, null, true),
                    ctx.RequestAborted);
                return Results.NoContent();
            });

        return app;
    }
}

internal sealed record UpsertPreferencesBody(
    bool? DmSoundEnabled,
    string? RoomSoundLevel,
    bool? DndEnabled,
    bool? BrowserNotificationsEnabled
);

internal sealed record SetOverrideBody(string Level);
internal sealed record MarkReadBody(IReadOnlyList<Guid> Ids);
