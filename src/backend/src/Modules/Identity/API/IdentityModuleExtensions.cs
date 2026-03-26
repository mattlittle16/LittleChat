using Identity.Application;
using Identity.Application.Commands;
using Identity.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddScoped<IUserSyncService, UserSyncService>();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SetUserStatusCommandHandler>());
        return services;
    }
}
