using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Commands;

namespace Notifications.API;

public static class NotificationsApiModuleExtensions
{
    public static IServiceCollection AddNotificationsApiModule(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<UpsertPreferencesCommandHandler>());
        return services;
    }

    public static WebApplication MapNotificationsEndpoints(this WebApplication app)
    {
        NotificationsEndpoints.MapNotificationsEndpoints(app);
        return app;
    }
}
