using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Contracts;
using StackExchange.Redis;

namespace LittleChat.Modules.Admin.Infrastructure.Middleware;

public sealed class TokenBlocklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _fallbackCache;

    // How long to retain a blocked-user entry in the in-memory fallback cache.
    // During a Redis outage, blocked users stay blocked for up to this duration.
    private static readonly TimeSpan FallbackTtl = TimeSpan.FromSeconds(30);

    public TokenBlocklistMiddleware(RequestDelegate next, IConnectionMultiplexer redis, IMemoryCache fallbackCache)
    {
        _next = next;
        _redis = redis;
        _fallbackCache = fallbackCache;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var userId = ctx.User.GetInternalUserId();
            if (userId.HasValue)
            {
                var cacheKey = $"blocklist:user:{userId.Value}";
                try
                {
                    var db = _redis.GetDatabase();
                    var blocked = await db.KeyExistsAsync(cacheKey);
                    if (blocked)
                    {
                        // Populate fallback cache so a subsequent Redis outage keeps this user blocked
                        _fallbackCache.Set(cacheKey, true, FallbackTtl);
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Session has been terminated.\"}");
                        return;
                    }
                    // User is not blocked — remove any stale fallback cache entry
                    _fallbackCache.Remove(cacheKey);
                }
                catch (Exception)
                {
                    // Redis unavailable — consult the in-memory fallback cache before failing open
                    if (_fallbackCache.TryGetValue(cacheKey, out _))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Session has been terminated.\"}");
                        return;
                    }
                    // Not in fallback cache — fail open to avoid disrupting all authenticated requests
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
