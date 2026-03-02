using Microsoft.Extensions.DependencyInjection;
using Reactions.Application.Commands;
using Reactions.Infrastructure;

namespace Reactions.API;

public static class ReactionsModuleExtensions
{
    public static IServiceCollection AddReactionsModule(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<AddReactionCommandHandler>());

        services.AddReactionsInfrastructure();

        return services;
    }
}
