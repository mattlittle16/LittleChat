using EnrichedMessaging.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace EnrichedMessaging.API;

public static class EnrichedMessagingApiExtensions
{
    public static IServiceCollection AddEnrichedMessagingApiModule(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<CreatePollCommandHandler>());
        return services;
    }

    public static WebApplication MapEnrichedMessagingEndpoints(this WebApplication app)
    {
        EnrichedMessagingEndpoints.MapEndpoints(app);
        return app;
    }
}
