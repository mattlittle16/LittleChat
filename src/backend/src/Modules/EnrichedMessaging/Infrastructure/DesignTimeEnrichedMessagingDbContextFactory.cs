using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnrichedMessaging.Infrastructure;

// Used by `dotnet ef migrations add` at design time
public sealed class DesignTimeEnrichedMessagingDbContextFactory : IDesignTimeDbContextFactory<EnrichedMessagingDbContext>
{
    public EnrichedMessagingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Database=littlechat;Username=postgres;Password=localdevpassword";

        var optionsBuilder = new DbContextOptionsBuilder<EnrichedMessagingDbContext>();
        optionsBuilder.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(EnrichedMessagingDbContext).Assembly.GetName().Name));

        return new EnrichedMessagingDbContext(optionsBuilder.Options);
    }
}
