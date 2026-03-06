using Shared.Contracts.Interfaces;
using StackExchange.Redis;

namespace Presence.Infrastructure;

public sealed class PresenceService : IPresenceService
{
    // Per-connection key TTL: 30 seconds. Clients must send a Heartbeat at least every 15s.
    // Key format: presence:{userId}:{connectionId}
    // A user is online if ANY of their connection keys exist.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IConnectionMultiplexer _redis;

    public PresenceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task SetOnlineAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return db.StringSetAsync(ConnKey(userId, connectionId), "1", Ttl);
    }

    public async Task<bool> SetOfflineAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(ConnKey(userId, connectionId));

        // User is fully offline only if no other connection keys remain
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        return !server.Keys(pattern: $"presence:{userId}:*").Any();
    }

    public Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        return Task.FromResult(server.Keys(pattern: $"presence:{userId}:*").Any());
    }

    public Task<IReadOnlyList<Guid>> GetAllOnlineAsync(CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        var result = new HashSet<Guid>();
        foreach (var key in server.Keys(pattern: "presence:*:*"))
        {
            // Format: presence:{userId}:{connectionId} — extract just the userId segment
            var raw = ((string)key!).AsSpan("presence:".Length);
            var colonIdx = raw.IndexOf(':');
            var userIdSpan = colonIdx >= 0 ? raw[..colonIdx] : raw;
            if (Guid.TryParse(userIdSpan, out var id))
                result.Add(id);
        }
        return Task.FromResult<IReadOnlyList<Guid>>(result.ToList());
    }

    private static string ConnKey(Guid userId, string connectionId) => $"presence:{userId}:{connectionId}";
}
