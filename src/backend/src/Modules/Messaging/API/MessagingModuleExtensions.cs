using Messaging.Application.Commands;
using Messaging.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.API;

public static class MessagingModuleExtensions
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SendMessageCommandHandler>());

        services.Configure<MessagingOptions>(opts =>
        {
            if (int.TryParse(configuration["MESSAGE_PAGE_SIZE"], out var size) && size > 0)
                opts.MessagePageSize = size;
        });

        return services;
    }
}
