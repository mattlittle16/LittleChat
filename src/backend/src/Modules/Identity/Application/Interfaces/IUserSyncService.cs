using System.Security.Claims;

namespace Identity.Application.Interfaces;

public interface IUserSyncService
{
    Task EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
