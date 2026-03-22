using System.Security.Claims;
using Bogus;
using Identity.Application;
using Identity.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace Tests.Unit.Identity;

public class UserSyncServiceTests
{
    private readonly IUserRepository _repo  = Substitute.For<IUserRepository>();
    private readonly IMemoryCache    _cache = new MemoryCache(new MemoryCacheOptions());
    private static readonly Faker Fake = new();

    private UserSyncService Build() => new(_repo, _cache);

    private static ClaimsPrincipal MakePrincipal(
        string? sub  = null,
        string? name = null,
        string? pic  = null)
    {
        var actualSub  = sub  ?? $"auth|{Fake.Random.AlphaNumeric(12)}";
        var actualName = name ?? Fake.Internet.UserName();
        var claims     = new List<Claim> { new("sub", actualSub), new("preferred_username", actualName) };
        if (pic is not null) claims.Add(new("picture", pic));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    private static string RandomSub() => $"auth|{Fake.Random.AlphaNumeric(12)}";

    [Fact]
    public async Task Throws_when_sub_claim_is_missing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().EnsureUserExistsAsync(principal));
    }

    [Fact]
    public async Task Cache_hit_returns_early_without_hitting_repo()
    {
        var userId    = Guid.NewGuid();
        var sub       = RandomSub();
        var principal = MakePrincipal(sub: sub);

        // Populate the cache manually so the first call is a hit
        _cache.Set($"user_synced:{sub}", userId);

        var (isNew, returnedId) = await Build().EnsureUserExistsAsync(principal);

        Assert.False(isNew);
        Assert.Equal(userId, returnedId);
        await _repo.DidNotReceive().UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_miss_upserts_user_and_returns_new_id()
    {
        var userId    = Guid.NewGuid();
        var sub       = RandomSub();
        var name      = Fake.Internet.UserName();
        var principal = MakePrincipal(sub: sub, name: name);

        _repo.UpsertAsync(sub, name, null, Arg.Any<CancellationToken>())
             .Returns((IsNew: true, UserId: userId));

        var (isNew, returnedId) = await Build().EnsureUserExistsAsync(principal);

        Assert.True(isNew);
        Assert.Equal(userId, returnedId);
    }

    [Fact]
    public async Task Second_call_uses_cache_and_does_not_upsert_again()
    {
        var userId    = Guid.NewGuid();
        var principal = MakePrincipal();

        _repo.UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
             .Returns((IsNew: false, UserId: userId));

        var svc = Build();
        await svc.EnsureUserExistsAsync(principal);
        await svc.EnsureUserExistsAsync(principal);

        await _repo.Received(1).UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
