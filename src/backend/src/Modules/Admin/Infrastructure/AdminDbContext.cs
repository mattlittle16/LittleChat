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
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(e => e.AdminId).HasColumnName("admin_id").IsRequired();
            entity.Property(e => e.AdminName).HasColumnName("admin_name").IsRequired();
            entity.Property(e => e.Action).HasColumnName("action").IsRequired();
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetName).HasColumnName("target_name");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired()
                .HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.OccurredAt)
                .IsDescending(true)
                .HasDatabaseName("idx_audit_log_occurred_at");
        });
    }
}
