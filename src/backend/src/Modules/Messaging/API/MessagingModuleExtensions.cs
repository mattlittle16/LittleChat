using Messaging.Application.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.API;

public static class MessagingModuleExtensions
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SendMessageCommandHandler>());

        return services;
    }
}
