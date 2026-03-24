using Microsoft.Extensions.Logging;
using Presence.Infrastructure;
using StackExchange.Redis;

namespace Tests.Unit.Presence;

public class PresenceServiceTests
{
    private readonly IConnectionMultiplexer _mux = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly ILogger<PresenceService> _logger = Substitute.For<ILogger<PresenceService>>();

    public PresenceServiceTests()
    {
        _mux.GetDatabase().Returns(_db);
    }

    private PresenceService Build() => new(_mux, _logger);

    // --- SetOfflineAsync ---

    [Fact]
    public async Task SetOfflineAsync_returns_false_when_script_returns_zero()
    {
        // Lua returns 0 → user still has active connections
        _db.ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object?>(), Arg.Any<CommandFlags>())
           .Returns(RedisResult.Create((RedisValue)0));

        var result = await Build().SetOfflineAsync(Guid.NewGuid(), "conn-1");

        Assert.False(result);
    }

    [Fact]
    public async Task SetOfflineAsync_returns_true_when_script_returns_one()
    {
        // Lua returns 1 → user has no remaining connections (fully offline)
        _db.ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object?>(), Arg.Any<CommandFlags>())
           .Returns(RedisResult.Create((RedisValue)1));

        var result = await Build().SetOfflineAsync(Guid.NewGuid(), "conn-1");

        Assert.True(result);
    }

    // --- GetAllOnlineAsync ---

    [Fact]
    public async Task GetAllOnlineAsync_returns_all_valid_guids()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)id1.ToString(), (RedisValue)id2.ToString()]);

        var result = await Build().GetAllOnlineAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(id1, result);
        Assert.Contains(id2, result);
    }

    [Fact]
    public async Task GetAllOnlineAsync_skips_non_guid_entries()
    {
        var validId = Guid.NewGuid();
        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([(RedisValue)validId.ToString(), (RedisValue)"not-a-guid", (RedisValue)""]);

        var result = await Build().GetAllOnlineAsync();

        Assert.Single(result);
        Assert.Equal(validId, result[0]);
    }

    [Fact]
    public async Task GetAllOnlineAsync_returns_empty_list_when_online_set_is_empty()
    {
        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns([]);

        var result = await Build().GetAllOnlineAsync();

        Assert.Empty(result);
    }

    // --- IsOnlineAsync ---

    [Fact]
    public async Task IsOnlineAsync_returns_true_when_redis_confirms_membership()
    {
        _db.SetContainsAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
           .Returns(true);

        var result = await Build().IsOnlineAsync(Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task IsOnlineAsync_returns_false_when_user_not_in_online_set()
    {
        _db.SetContainsAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
           .Returns(false);

        var result = await Build().IsOnlineAsync(Guid.NewGuid());

        Assert.False(result);
    }

    // --- Retry / resilience ---

    [Fact]
    public async Task SetOnlineAsync_retries_and_succeeds_after_transient_redis_exception()
    {
        _db.ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object?>(), Arg.Any<CommandFlags>())
           .Returns(
               Task.FromException<RedisResult>(new RedisException("transient failure")),
               Task.FromResult(RedisResult.Create((RedisValue)1)));

        // Should not throw — single retry succeeds
        await Build().SetOnlineAsync(Guid.NewGuid(), "conn-1");
    }

    [Fact]
    public async Task SetOnlineAsync_does_not_throw_when_all_retries_exhausted()
    {
        // All attempts fail; service must swallow and log — Redis blips must not crash connections
        _db.ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object?>(), Arg.Any<CommandFlags>())
           .Returns(Task.FromException<RedisResult>(new RedisException("persistent failure")));

        await Build().SetOnlineAsync(Guid.NewGuid(), "conn-1");
        // No exception → test passes
    }
}
