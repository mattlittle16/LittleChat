using System.Security.Claims;
using Identity.Application.Interfaces;
using Identity.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace Identity.Application;

public sealed class UserSyncService : IUserSyncService
{
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _cache;

    public UserSyncService(IUserRepository userRepository, IMemoryCache cache)
    {
        _userRepository = userRepository;
        _cache = cache;
    }

    public async Task<bool> EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var sub = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT is missing 'sub' claim.");

        if (!Guid.TryParse(sub, out var userId))
            throw new InvalidOperationException($"'sub' claim is not a valid UUID: {sub}");

        var cacheKey = $"user_synced:{sub}";

        // If we've already synced this user in this process lifetime, skip the DB hit.
        // We use a short TTL so display name changes from Authentik propagate within ~1 minute.
        if (_cache.TryGetValue(cacheKey, out _))
            return false;

        var displayName = principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("name")
            ?? "Unknown";
        var avatarUrl = principal.FindFirstValue("picture");

        var isNew = await _userRepository.UpsertAsync(userId, displayName, avatarUrl, cancellationToken);

        _cache.Set(cacheKey, true, TimeSpan.FromMinutes(1));

        return isNew;
    }
}
