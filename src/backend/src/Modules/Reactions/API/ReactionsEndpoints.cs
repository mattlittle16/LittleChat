using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reactions.Application.Commands;

namespace Reactions.API;

public static class ReactionsEndpoints
{
    public sealed record AddReactionBody(string Emoji);

    public static IEndpointRouteBuilder MapReactionsEndpoints(this IEndpointRouteBuilder app)
    {
        // REST fallback for reaction toggle
        app.MapPost("/api/rooms/{roomId}/messages/{messageId}/reactions",
            [Authorize] async (
                HttpContext ctx,
                ISender sender,
                Guid roomId,
                Guid messageId,
                AddReactionBody body) =>
            {
                if (string.IsNullOrWhiteSpace(body.Emoji))
                    return Results.Problem(
                        title: "Missing emoji",
                        detail: "The 'emoji' field is required.",
                        statusCode: 400);

                var sub = ctx.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Unauthorized();

                var displayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";

                var (added, count) = await sender.Send(new AddReactionCommand(
                    MessageId: messageId,
                    RoomId: roomId,
                    UserId: userId,
                    DisplayName: displayName,
                    Emoji: body.Emoji));

                return Results.Ok(new { added, count });
            });

        return app;
    }
}
