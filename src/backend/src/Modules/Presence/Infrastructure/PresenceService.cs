using Shared.Contracts.Interfaces;
using StackExchange.Redis;

namespace Presence.Infrastructure;

public sealed class PresenceService : IPresenceService
{
    // Key TTL: 30 seconds. Clients must send a Heartbeat at least every 15s.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IConnectionMultiplexer _redis;

    public PresenceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task SetOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return db.StringSetAsync(Key(userId), "1", Ttl);
    }

    public Task SetOfflineAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return db.KeyDeleteAsync(Key(userId));
    }

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync(Key(userId));
    }

    private static string Key(Guid userId) => $"presence:{userId}";
}
