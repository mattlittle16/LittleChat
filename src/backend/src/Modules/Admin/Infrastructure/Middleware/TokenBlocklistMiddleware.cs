using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shared.Contracts;
using StackExchange.Redis;

namespace LittleChat.Modules.Admin.Infrastructure.Middleware;

public sealed class TokenBlocklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;

    public TokenBlocklistMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var userId = ctx.User.GetInternalUserId();
            if (userId.HasValue)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var blocked = await db.KeyExistsAsync($"blocklist:user:{userId.Value}");
                    if (blocked)
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Session has been terminated.\"}");
                        return;
                    }
                }
                catch (Exception)
                {
                    // Redis unavailable — fail open to avoid disrupting all authenticated requests.
                }
            }
        }

        await _next(ctx);
    }
}

public static class TokenBlocklistMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenBlocklist(this IApplicationBuilder app)
        => app.UseMiddleware<TokenBlocklistMiddleware>();
}
