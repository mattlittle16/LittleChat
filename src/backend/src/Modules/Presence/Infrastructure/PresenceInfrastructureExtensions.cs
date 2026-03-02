using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Interfaces;

namespace Presence.Infrastructure;

public static class PresenceInfrastructureExtensions
{
    public static IServiceCollection AddPresenceInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPresenceService, PresenceService>();
        return services;
    }
}
