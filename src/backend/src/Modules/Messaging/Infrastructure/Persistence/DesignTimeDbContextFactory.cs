using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Messaging.Infrastructure.Persistence;

// Used by `dotnet ef migrations add` at design time
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LittleChatDbContext>
{
    public LittleChatDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Database=littlechat;Username=postgres;Password=localdevpassword";

        var optionsBuilder = new DbContextOptionsBuilder<LittleChatDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LittleChatDbContext(optionsBuilder.Options);
    }
}
