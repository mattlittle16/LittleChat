using Microsoft.Extensions.DependencyInjection;
using Search.Application.Queries;
using Search.Infrastructure;

namespace Search.API;

public static class SearchModuleExtensions
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SearchQueryHandler>());

        services.AddSearchInfrastructure();

        return services;
    }
}
