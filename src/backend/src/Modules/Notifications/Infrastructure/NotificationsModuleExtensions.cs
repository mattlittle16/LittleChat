using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Domain;

namespace Notifications.Infrastructure;

public static class NotificationsModuleExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(NotificationsDbContext).Assembly.GetName().Name)));

        services.AddScoped<INotificationPreferencesRepository, NotificationPreferencesRepository>();
        services.AddScoped<IConversationOverrideRepository, ConversationOverrideRepository>();

        return services;
    }
}
