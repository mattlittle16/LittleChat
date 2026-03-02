using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Files.API;

public static class FilesModuleExtensions
{
    public static IServiceCollection AddFilesModule(this IServiceCollection services, IConfiguration configuration)
    {
        Files.Infrastructure.FilesInfrastructureExtensions.AddFilesInfrastructure(services);
        return services;
    }
}
