using Microsoft.Extensions.Logging;
using Shared.Contracts.Interfaces;
using StackExchange.Redis;

namespace Presence.Infrastructure;

public sealed class PresenceService : IPresenceService
{
    // Design: per-user connection Set + online Set.
    //
    // presence:connections:{userId} — Set   members={connectionId strings}
    // presence:online               — Set   members={userId strings}
    //
    // Each SignalR connection adds its connectionId to the user's connection Set on connect
    // and removes it on disconnect. The user enters the online Set when their first connection
    // is added (SCARD 0→1) and leaves when their last connection is removed (SCARD N→0).
    //
    // Because SREM is a no-op for members that are not in the Set, a stale OnDisconnectedAsync
    // for a connection that was never registered (e.g. arriving after a Redis clear + reconnect)
    // cannot decrement a fresh connection's count. This eliminates the race condition where a
    // page reload would leave a user stuck offline while still actively connected.

    private const string OnlineSetKey = "presence:online";

    // Lua scripts execute atomically on the Redis server.

    // Adds connectionId to the user's connection Set.
    // If this is the first connection (SCARD was 0), also adds userId to the online Set.
    private static readonly LuaScript SetOnlineScript = LuaScript.Prepare("""
        redis.call('SADD', @connKey, @connectionId)
        local count = redis.call('SCARD', @connKey)
        if count == 1 then
            redis.call('SADD', @onlineKey, @userId)
        end
        return count
        """);

    // Clears the user's connection Set and re-adds only this connectionId.
    // Used by clients on reconnect to recover from stale state after a server crash.
    private static readonly LuaScript ReassertScript = LuaScript.Prepare("""
        redis.call('DEL', @connKey)
        redis.call('SADD', @connKey, @connectionId)
        redis.call('SADD', @onlineKey, @userId)
        """);

    // Removes connectionId from the user's connection Set.
    // If the Set is now empty, removes userId from the online Set and returns 1 (fully offline).
    // If connectionId was not in the Set (stale disconnect), SREM is a no-op — SCARD is still
    // ≥1, so 0 is returned and no offline broadcast is triggered.
    private static readonly LuaScript SetOfflineScript = LuaScript.Prepare("""
        redis.call('SREM', @connKey, @connectionId)
        local count = redis.call('SCARD', @connKey)
        if count == 0 then
            redis.call('DEL', @connKey)
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
                connKey      = (RedisKey)ConnSetKey(userId),
                onlineKey    = (RedisKey)OnlineSetKey,
                userId       = (RedisValue)userId.ToString(),
                connectionId = (RedisValue)connectionId,
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
                connKey      = (RedisKey)ConnSetKey(userId),
                onlineKey    = (RedisKey)OnlineSetKey,
                userId       = (RedisValue)userId.ToString(),
                connectionId = (RedisValue)connectionId,
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

    public async Task ReassertAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        await WithRetryAsync(async () =>
        {
            var db = _redis.GetDatabase();
            await db.ScriptEvaluateAsync(ReassertScript, new
            {
                connKey      = (RedisKey)ConnSetKey(userId),
                onlineKey    = (RedisKey)OnlineSetKey,
                userId       = (RedisValue)userId.ToString(),
                connectionId = (RedisValue)connectionId,
            });
        }, nameof(ReassertAsync), userId);
    }

    /// <summary>
    /// Clears all presence state. Called on startup to discard stale entries from a previous
    /// server run or crash. Clients will re-establish their presence as they reconnect.
    /// </summary>
    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        // Collect all users currently marked online so we can delete their connection Sets.
        var onlineMembers = await db.SetMembersAsync(OnlineSetKey);

        var keysToDelete = new List<RedisKey>(onlineMembers.Length + 2)
        {
            OnlineSetKey,
            // Also remove any legacy refcount key from the old integer-based implementation.
            "presence:refcount",
        };

        foreach (var member in onlineMembers)
            keysToDelete.Add(ConnSetKey(member.ToString()));

        await db.KeyDeleteAsync(keysToDelete.ToArray());
    }

    private static string ConnSetKey(Guid userId)   => $"presence:connections:{userId}";
    private static string ConnSetKey(string userId) => $"presence:connections:{userId}";

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
