using Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        return app;
    }
}
