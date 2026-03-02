using Microsoft.Extensions.DependencyInjection;
using Reactions.Application;

namespace Reactions.Infrastructure;

public static class ReactionsInfrastructureExtensions
{
    public static IServiceCollection AddReactionsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IReactionRepository, ReactionRepository>();
        return services;
    }
}
