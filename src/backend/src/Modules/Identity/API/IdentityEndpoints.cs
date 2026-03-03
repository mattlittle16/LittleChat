using Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts;
using Shared.Contracts.Interfaces;

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
            var userId = ctx.User.GetInternalUserId();
            if (userId is null)
                return Results.Unauthorized();

            var user = await users.GetByIdAsync(userId.Value, ctx.RequestAborted);
            return user is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    id          = user.Id,
                    displayName = user.DisplayName,
                    avatarUrl   = user.AvatarUrl,
                    createdAt   = user.CreatedAt,
                });
        });

        // Authenticated — returns all users with online status; supports ?q= name filter
        app.MapGet("/api/users", [Authorize] async (HttpContext ctx, IUserRepository users,
            IPresenceService presence, string? q) =>
        {
            var currentUserId = ctx.User.GetInternalUserId();
            if (currentUserId is null)
                return Results.Unauthorized();

            var allUsers = await users.GetAllAsync(q, ctx.RequestAborted);

            var result = new List<object>(allUsers.Count);
            foreach (var user in allUsers)
            {
                if (user.Id == currentUserId.Value) continue;

                var isOnline = await presence.IsOnlineAsync(user.Id, ctx.RequestAborted);
                result.Add(new
                {
                    id          = user.Id,
                    displayName = user.DisplayName,
                    avatarUrl   = user.AvatarUrl,
                    isOnline,
                });
            }

            return Results.Ok(result);
        });

        return app;
    }
}
