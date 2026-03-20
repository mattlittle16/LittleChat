using LittleChat.Modules.Admin.Application;
using LittleChat.Modules.Admin.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LittleChat.Modules.Admin.Infrastructure;

public static class AdminInfrastructureExtensions
{
    public static IServiceCollection AddAdminInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");

        services.AddDbContext<AdminDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AdminDbContext).Assembly.GetName().Name)));

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<LittleChat.Modules.Admin.Application.ITokenBlocklistService, TokenBlocklistService>();
        services.AddScoped<IAdminRepository, AdminRepository>();

        return services;
    }
}
