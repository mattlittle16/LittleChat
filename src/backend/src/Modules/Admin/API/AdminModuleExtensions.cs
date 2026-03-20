using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LittleChat.Modules.Admin.API;

public static class AdminModuleExtensions
{
    public const string PolicyName = "AdminPolicy";

    public static IServiceCollection AddAdminModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AdminClaimOptions>()
            .Configure(options =>
            {
                options.ClaimField = configuration["ADMIN_CLAIM_FIELD"] ?? "groups";
                options.ClaimValues = configuration["ADMIN_CLAIM_VALUES"] ?? "app-admin";
            });

        services.AddSingleton<IAuthorizationHandler, AdminRequirementHandler>();
        services.AddAuthorization(options =>
            options.AddPolicy(PolicyName, policy => policy.AddRequirements(new AdminRequirement())));

        services.AddAdminInfrastructure(configuration);

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ForceLogoutUserCommandHandler>());

        return services;
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        AdminEndpoints.Map(app);
        return app;
    }
}
