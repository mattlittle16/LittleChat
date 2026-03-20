using LittleChat.Modules.Admin.Application;
using StackExchange.Redis;

namespace LittleChat.Modules.Admin.Infrastructure;

public sealed class TokenBlocklistService : ITokenBlocklistService
{
    private readonly IConnectionMultiplexer _redis;
    public TokenBlocklistService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task BlockUserAsync(Guid internalUserId, TimeSpan banDuration, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"blocklist:user:{internalUserId}", "1", banDuration);
    }

    public async Task<DateTimeOffset?> GetBanExpiryAsync(Guid internalUserId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync($"blocklist:user:{internalUserId}");
        if (ttl is null) return null;
        return DateTimeOffset.UtcNow.Add(ttl.Value);
    }

    public async Task UnblockUserAsync(Guid internalUserId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"blocklist:user:{internalUserId}");
    }
}
