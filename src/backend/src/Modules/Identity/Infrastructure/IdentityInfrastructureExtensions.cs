using Identity.Domain;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Interfaces;

namespace Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserLookupService>(sp => (IUserLookupService)sp.GetRequiredService<IUserRepository>());
        return services;
    }
}
