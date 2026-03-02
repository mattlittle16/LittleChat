using Microsoft.Extensions.DependencyInjection;

namespace Search.API;

public static class SearchModuleExtensions
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services)
    {
        return services;
    }
}
