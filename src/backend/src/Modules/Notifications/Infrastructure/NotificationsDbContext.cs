using Notifications.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Notifications.Infrastructure;

public sealed class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<NotificationPreferencesEntity> NotificationPreferences => Set<NotificationPreferencesEntity>();
    public DbSet<ConversationOverrideEntity> ConversationOverrides => Set<ConversationOverrideEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
