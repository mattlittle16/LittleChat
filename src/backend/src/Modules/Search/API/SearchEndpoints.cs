using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Search.Application.Queries;

namespace Search.API;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/search?q=hello&scope=room&roomId=<guid>
        // GET /api/search?q=hello&scope=global
        app.MapGet("/api/search", [Authorize] async (
            HttpContext ctx,
            ISender sender,
            string? q,
            string? scope,
            Guid? roomId) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.Problem(
                    title: "Missing query parameter",
                    detail: "The 'q' parameter is required and must not be empty.",
                    statusCode: 400);

            var resolvedScope = scope?.ToLowerInvariant() is "room" or "global"
                ? scope.ToLowerInvariant()
                : "global";

            if (resolvedScope == "room" && roomId is null)
                return Results.Problem(
                    title: "Missing roomId",
                    detail: "The 'roomId' parameter is required when scope is 'room'.",
                    statusCode: 400);

            var sub = ctx.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            var results = await sender.Send(new SearchQuery(
                UserId: userId,
                Q: q,
                Scope: resolvedScope,
                RoomId: roomId), ctx.RequestAborted);

            return Results.Ok(results);
        });

        return app;
    }
}
