using Microsoft.Extensions.DependencyInjection;

namespace Reactions.API;

public static class ReactionsModuleExtensions
{
    public static IServiceCollection AddReactionsModule(this IServiceCollection services)
    {
        return services;
    }
}
