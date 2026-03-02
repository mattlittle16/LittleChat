using Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StackExchange.Redis;

namespace Identity.API;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        // Public — initiates browser OIDC redirect to Authentik
        app.MapGet("/auth/login", (HttpContext ctx) =>
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = "/auth/callback",
            };
            return Results.Challenge(props, ["OpenIdConnect"]);
        }).AllowAnonymous();

        // Authenticated — returns current user's profile
        app.MapGet("/api/users/me", [Authorize] async (HttpContext ctx, IUserRepository users) =>
        {
            var sub = ctx.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            var user = await users.GetByIdAsync(userId, ctx.RequestAborted);
            return user is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    id = user.Id,
                    displayName = user.DisplayName,
                    avatarUrl = user.AvatarUrl,
                    createdAt = user.CreatedAt,
                });
        });

        // Authenticated — returns all users with online status; supports ?q= name filter
        app.MapGet("/api/users", [Authorize] async (HttpContext ctx, IUserRepository users,
            IConnectionMultiplexer redis, string? q) =>
        {
            var sub = ctx.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentUserId))
                return Results.Unauthorized();

            var allUsers = await users.GetAllAsync(q, ctx.RequestAborted);
            var db = redis.GetDatabase();

            var result = new List<object>(allUsers.Count);
            foreach (var user in allUsers)
            {
                // Skip self from the list
                if (user.Id == currentUserId) continue;

                var isOnline = await db.KeyExistsAsync($"presence:{user.Id}");
                result.Add(new
                {
                    id = user.Id,
                    displayName = user.DisplayName,
                    avatarUrl = user.AvatarUrl,
                    isOnline,
                });
            }

            return Results.Ok(result);
        });

        return app;
    }
}
