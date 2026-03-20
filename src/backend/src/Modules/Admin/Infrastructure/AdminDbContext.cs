using LittleChat.Modules.Admin.Domain;
using Microsoft.EntityFrameworkCore;

namespace LittleChat.Modules.Admin.Infrastructure;

public sealed class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("admin_audit_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.AdminId).IsRequired();
            entity.Property(e => e.AdminName).IsRequired();
            entity.Property(e => e.Action).IsRequired();
            entity.Property(e => e.TargetId);
            entity.Property(e => e.TargetName);
            entity.Property(e => e.OccurredAt).IsRequired()
                .HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.OccurredAt)
                .IsDescending(true)
                .HasDatabaseName("idx_audit_log_occurred_at");
        });
    }
}
