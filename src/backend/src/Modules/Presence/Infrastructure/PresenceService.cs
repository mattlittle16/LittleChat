using Microsoft.Extensions.Logging;
using Shared.Contracts.Interfaces;
using StackExchange.Redis;

namespace Presence.Infrastructure;

public sealed class PresenceService : IPresenceService
{
    // Design: refcount Hash + online Set.
    //
    // presence:refcount  — Hash  field={userId}  value={open connection count}
    // presence:online    — Set   members={userId strings}
    //
    // Multi-tab support: refcount tracks how many connections a user has open.
    // The Set is the fast lookup structure: O(1) SISMEMBER, O(n-online) SMEMBERS.
    // Both structures are cleared on hub startup to recover from server crashes.
    //
    // Heartbeat calls SetOnlineAsync again — this is now a no-op for already-connected
    // users (refcount is only incremented on the first connection, not refreshed).
    // The SignalR infrastructure detects broken WebSocket connections and fires
    // OnDisconnectedAsync, so TTL-based expiry is no longer needed.

    private const string RefCountKey = "presence:refcount";
    private const string OnlineSetKey = "presence:online";

    // Lua scripts execute atomically on the Redis server — no other command can interleave
    // between the HINCRBY and the SADD/SREM, eliminating the inconsistency window that exists
    // when those are sent as two separate commands.
    private static readonly LuaScript SetOnlineScript = LuaScript.Prepare("""
        local count = redis.call('HINCRBY', @refKey, @userId, 1)
        if count == 1 then
            redis.call('SADD', @onlineKey, @userId)
        end
        return count
        """);

    // Forces refcount to 1 and adds user to the online set.
    // Used by clients on reconnect to recover from stale refcounts after a server crash.
    // Note: if a user has multiple tabs open and all reconnect simultaneously, the last
    // ReassertAsync wins and refcount is 1 — subsequent disconnects may leave stale online state
    // until the final tab closes. Acceptable for a single-instance deployment.
    private static readonly LuaScript ReassertScript = LuaScript.Prepare("""
        redis.call('HSET', @refKey, @userId, 1)
        redis.call('SADD', @onlineKey, @userId)
        """);

    // Returns 1 if the user is now fully offline (all connections closed), 0 otherwise.
    private static readonly LuaScript SetOfflineScript = LuaScript.Prepare("""
        local count = redis.call('HINCRBY', @refKey, @userId, -1)
        if count <= 0 then
            redis.call('HDEL', @refKey, @userId)
            redis.call('SREM', @onlineKey, @userId)
            return 1
        end
        return 0
        """);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<PresenceService> _logger;

    public PresenceService(IConnectionMultiplexer redis, ILogger<PresenceService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SetOnlineAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        await WithRetryAsync(async () =>
        {
            var db = _redis.GetDatabase();
            await db.ScriptEvaluateAsync(SetOnlineScript, new
            {
                refKey    = (RedisKey)RefCountKey,
                onlineKey = (RedisKey)OnlineSetKey,
                userId    = (RedisValue)userId.ToString(),
            });
        }, nameof(SetOnlineAsync), userId);
    }

    public async Task<bool> SetOfflineAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        var fullyOffline = false;
        await WithRetryAsync(async () =>
        {
            var db = _redis.GetDatabase();
            var result = await db.ScriptEvaluateAsync(SetOfflineScript, new
            {
                refKey    = (RedisKey)RefCountKey,
                onlineKey = (RedisKey)OnlineSetKey,
                userId    = (RedisValue)userId.ToString(),
            });
            fullyOffline = (int)result == 1;
        }, nameof(SetOfflineAsync), userId);

        return fullyOffline;
    }

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return await db.SetContainsAsync(OnlineSetKey, userId.ToString());
    }

    public async Task<IReadOnlyList<Guid>> GetAllOnlineAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var members = await db.SetMembersAsync(OnlineSetKey);
        var result = new List<Guid>(members.Length);
        foreach (var m in members)
        {
            if (Guid.TryParse(m, out var id))
                result.Add(id);
        }
        return result;
    }

    public async Task ReassertAsync(Guid userId, CancellationToken ct = default)
    {
        await WithRetryAsync(async () =>
        {
            var db = _redis.GetDatabase();
            await db.ScriptEvaluateAsync(ReassertScript, new
            {
                refKey    = (RedisKey)RefCountKey,
                onlineKey = (RedisKey)OnlineSetKey,
                userId    = (RedisValue)userId.ToString(),
            });
        }, nameof(ReassertAsync), userId);
    }

    /// <summary>
    /// Clears all presence state. Call once on hub startup to discard stale entries
    /// from a previous server run or crash.
    /// </summary>
    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync([RefCountKey, OnlineSetKey]);
    }

    // Retries the Redis operation up to 3 times with exponential backoff (200ms, 400ms, 800ms).
    // On final failure, logs a warning and continues — a Redis blip must not crash connections.
    private async Task WithRetryAsync(Func<Task> operation, string operationName, Guid userId)
    {
        int[] delaysMs = [200, 400, 800];
        for (var attempt = 0; attempt <= delaysMs.Length; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (RedisException ex) when (attempt < delaysMs.Length)
            {
                _logger.LogWarning(ex,
                    "Presence Redis operation {Op} for user {UserId} failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                    operationName, userId, attempt + 1, delaysMs.Length + 1, delaysMs[attempt]);
                await Task.Delay(delaysMs[attempt]);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex,
                    "Presence Redis operation {Op} for user {UserId} failed after all retries — presence may be inaccurate",
                    operationName, userId);
            }
        }
    }
}
