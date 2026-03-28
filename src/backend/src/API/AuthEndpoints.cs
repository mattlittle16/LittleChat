using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace API;

public static class AuthEndpoints
{
    internal static CookieOptions RefreshTokenCookieOptions(bool isHttps) => new()
    {
        HttpOnly = true,
        Secure   = isHttps,
        SameSite = SameSiteMode.Strict,
        Path     = "/auth/refresh",
    };

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Public — exchanges the HttpOnly refresh token cookie for a new access token.
        // The refresh token never leaves the server; only the new access token is returned to the client.
        app.MapPost("/auth/refresh", async (
            HttpContext ctx,
            IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
            IHttpClientFactory httpClientFactory) =>
        {
            var rt = ctx.Request.Cookies["littlechat_rt"];
            if (string.IsNullOrEmpty(rt))
                return Results.Unauthorized();

            var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
            var config  = await options.ConfigurationManager!.GetConfigurationAsync(ctx.RequestAborted);

            using var http          = httpClientFactory.CreateClient();
            var tokenResponse = await http.PostAsync(config.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"]    = "refresh_token",
                    ["refresh_token"] = rt,
                    ["client_id"]     = options.ClientId!,
                    ["client_secret"] = options.ClientSecret!,
                }), ctx.RequestAborted);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                ctx.Response.Cookies.Delete("littlechat_rt", new CookieOptions { Path = "/auth/refresh" });
                return Results.Unauthorized();
            }

            var json           = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ctx.RequestAborted);
            var newAccessToken = json.GetProperty("access_token").GetString()!;

            // Rotate refresh token cookie if Authentik issued a new one
            if (json.TryGetProperty("refresh_token", out var newRt) && !string.IsNullOrEmpty(newRt.GetString()))
                ctx.Response.Cookies.Append("littlechat_rt", newRt.GetString()!, RefreshTokenCookieOptions(ctx.Request.IsHttps));

            return Results.Ok(new { access_token = newAccessToken });
        }).AllowAnonymous();

        return app;
    }
}
