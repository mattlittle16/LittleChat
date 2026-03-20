using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LittleChat.Modules.Admin.API;

public sealed class AdminRequirementHandler : AuthorizationHandler<AdminRequirement>
{
    private readonly AdminClaimOptions _options;

    public AdminRequirementHandler(IOptions<AdminClaimOptions> options)
    {
        _options = options.Value;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var allowedValues = _options.ParsedClaimValues;
        var claimValues = context.User.FindAll(_options.ClaimField).Select(c => c.Value);
        var isAdmin = claimValues.Any(v =>
            v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Any(part => allowedValues.Contains(part, StringComparer.OrdinalIgnoreCase)));

        if (isAdmin)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
