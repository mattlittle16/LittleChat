using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Infrastructure.Persistence.Configurations;

public sealed class SidebarGroupConfiguration : IEntityTypeConfiguration<SidebarGroupEntity>
{
    public void Configure(EntityTypeBuilder<SidebarGroupEntity> builder)
    {
        builder.ToTable("sidebar_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(g => g.UserId).HasColumnName("user_id");
        builder.Property(g => g.Name).HasColumnName("name").IsRequired().HasMaxLength(50);
        builder.Property(g => g.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(g => g.IsCollapsed).HasColumnName("is_collapsed").HasDefaultValue(false);
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne(g => g.User).WithMany().HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.UserId).HasDatabaseName("idx_sidebar_groups_user_id");
    }
}
