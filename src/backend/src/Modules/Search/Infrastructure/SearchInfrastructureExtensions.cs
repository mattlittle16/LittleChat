using Microsoft.Extensions.DependencyInjection;
using Search.Application;

namespace Search.Infrastructure;

public static class SearchInfrastructureExtensions
{
    public static IServiceCollection AddSearchInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IMessageSearchRepository, MessageSearchRepository>();
        return services;
    }
}
