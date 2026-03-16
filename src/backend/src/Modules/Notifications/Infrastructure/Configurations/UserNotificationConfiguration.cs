using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Infrastructure.Entities;

namespace Notifications.Infrastructure.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotificationEntity>
{
    public void Configure(EntityTypeBuilder<UserNotificationEntity> builder)
    {
        builder.ToTable("user_notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();
        builder.Property(e => e.Type).HasColumnName("type").IsRequired();
        builder.Property(e => e.MessageId).HasColumnName("message_id");
        builder.Property(e => e.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(e => e.RoomName).HasColumnName("room_name").IsRequired().HasDefaultValue(string.Empty);
        builder.Property(e => e.FromUserId).HasColumnName("from_user_id");
        builder.Property(e => e.FromDisplayName).HasColumnName("from_display_name").IsRequired().HasDefaultValue(string.Empty);
        builder.Property(e => e.ContentPreview).HasColumnName("content_preview").IsRequired().HasDefaultValue(string.Empty);
        builder.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").HasDefaultValueSql("NOW() + INTERVAL '30 days'");

        builder.HasIndex(e => new { e.RecipientUserId, e.CreatedAt })
               .HasDatabaseName("ix_user_notifications_recipient_created")
               .IsDescending(false, true);
    }
}
