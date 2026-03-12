using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Infrastructure.Persistence.Configurations;

public sealed class RoomMembershipConfiguration : IEntityTypeConfiguration<RoomMembershipEntity>
{
    public void Configure(EntityTypeBuilder<RoomMembershipEntity> builder)
    {
        builder.ToTable("room_memberships");
        builder.HasKey(m => new { m.UserId, m.RoomId });
        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.Property(m => m.RoomId).HasColumnName("room_id");
        builder.Property(m => m.LastReadAt).HasColumnName("last_read_at").HasDefaultValueSql("NOW()");
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("NOW()");

        builder.Property(m => m.SidebarGroupId).HasColumnName("sidebar_group_id");

        builder.HasOne(m => m.User).WithMany(u => u.Memberships).HasForeignKey(m => m.UserId);
        builder.HasOne(m => m.Room).WithMany(r => r.Memberships).HasForeignKey(m => m.RoomId);
        builder.HasOne(m => m.SidebarGroup).WithMany(g => g.Memberships)
            .HasForeignKey(m => m.SidebarGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
