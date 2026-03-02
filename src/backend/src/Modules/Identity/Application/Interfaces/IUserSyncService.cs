using System.Security.Claims;

namespace Identity.Application.Interfaces;

public interface IUserSyncService
{
    /// <summary>
    /// Ensures the user exists in the database. Returns true if the user was newly created.
    /// Results are cached in IMemoryCache to avoid repeat DB hits within the same request.
    /// </summary>
    Task<bool> EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
