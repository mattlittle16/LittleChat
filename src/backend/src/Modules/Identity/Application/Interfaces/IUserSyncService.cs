using System.Security.Claims;

namespace Identity.Application.Interfaces;

public interface IUserSyncService
{
    /// <summary>
    /// Ensures the user exists in the database. Returns the internal user ID and whether
    /// the user was newly created. Results are cached in IMemoryCache to avoid repeat DB hits.
    /// </summary>
    Task<(bool IsNew, Guid UserId)> EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
