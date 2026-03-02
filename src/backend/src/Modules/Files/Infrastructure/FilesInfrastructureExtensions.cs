using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Interfaces;

namespace Files.Infrastructure;

public static class FilesInfrastructureExtensions
{
    public static IServiceCollection AddFilesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        return services;
    }
}
