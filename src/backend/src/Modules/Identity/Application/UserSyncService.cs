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

    public async Task<(bool IsNew, Guid UserId)> EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var sub = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT is missing 'sub' claim.");

        var cacheKey = $"user_synced:{sub}";

        // If we've already synced this user recently, skip the DB hit and return the cached ID.
        // Short TTL so display name / avatar changes from Authentik propagate within ~1 minute.
        if (_cache.TryGetValue(cacheKey, out Guid cachedUserId))
            return (false, cachedUserId);

        var rawDisplayName = principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("name")
            ?? "Unknown";

        // Strip @ from IdP-supplied names — email addresses are common as preferred_username
        // and would break the @mention system. Take the local part before the first @.
        var displayName = rawDisplayName.Contains('@')
            ? (rawDisplayName.IndexOf('@') > 0
                ? rawDisplayName[..rawDisplayName.IndexOf('@')]
                : rawDisplayName.Replace("@", ""))
            : rawDisplayName;

        var avatarUrl = principal.FindFirstValue("picture");

        var (isNew, userId) = await _userRepository.UpsertAsync(sub, displayName, avatarUrl, cancellationToken);

        _cache.Set(cacheKey, userId, TimeSpan.FromMinutes(1));

        return (isNew, userId);
    }
}
