using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace LittleChat.Modules.Admin.API;

public sealed class AdminAuthorizationService : IAdminAuthorizationService
{
    private readonly AdminClaimOptions _options;

    public AdminAuthorizationService(IOptions<AdminClaimOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAdmin(ClaimsPrincipal user)
    {
        var allowedValues = _options.ParsedClaimValues;
        var claimValues = user.FindAll(_options.ClaimField).Select(c => c.Value);
        return claimValues.Any(v =>
            v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Any(part => allowedValues.Contains(part, StringComparer.OrdinalIgnoreCase)));
    }
}
