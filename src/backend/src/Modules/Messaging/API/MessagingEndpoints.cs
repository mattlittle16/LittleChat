using MediatR;
using Messaging.Application.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Messaging.API;

public static class MessagingEndpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        // REST fallback for message send (e.g., when SignalR is unavailable)
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
