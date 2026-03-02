using Microsoft.Extensions.DependencyInjection;

namespace Presence.API;

public static class PresenceModuleExtensions
{
    public static IServiceCollection AddPresenceModule(this IServiceCollection services)
    {
        return services;
    }
}
