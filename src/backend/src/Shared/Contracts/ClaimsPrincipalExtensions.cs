using System.Security.Claims;

namespace Shared.Contracts;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the application's internal user ID (a UUID), injected as NameIdentifier
    /// during JWT validation. Distinct from the identity provider's raw 'sub' claim.
    /// </summary>
    public static Guid? GetInternalUserId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
