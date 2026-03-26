using EnrichedMessaging.Application.Services;
using EnrichedMessaging.Domain;
using EnrichedMessaging.Infrastructure.Repositories;
using EnrichedMessaging.Infrastructure.Services;
using Shared.Contracts.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnrichedMessaging.Infrastructure;

public static class EnrichedMessagingModuleExtensions
{
    public static IServiceCollection AddEnrichedMessagingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");

        services.AddDbContext<EnrichedMessagingDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(EnrichedMessagingDbContext).Assembly.GetName().Name)));

        services.AddHttpClient<ILinkPreviewFetcher, LinkPreviewFetcherService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddScoped<IPollRepository, PollRepository>();
        services.AddScoped<IHighlightRepository, HighlightRepository>();
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();
        services.AddScoped<ILinkPreviewRepository, LinkPreviewRepository>();
        services.AddScoped<IDigestRepository, DigestRepository>();
        services.AddScoped<ILinkPreviewReader, LinkPreviewReaderService>();
        services.AddScoped<IPollDataReader, PollDataReaderService>();

        return services;
    }
}
