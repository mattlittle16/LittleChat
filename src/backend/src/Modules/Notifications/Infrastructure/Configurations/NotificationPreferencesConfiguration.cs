using Notifications.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Notifications.Infrastructure.Configurations;

public sealed class NotificationPreferencesConfiguration : IEntityTypeConfiguration<NotificationPreferencesEntity>
{
    public void Configure(EntityTypeBuilder<NotificationPreferencesEntity> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(e => e.UserId);
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.DmSoundEnabled).HasColumnName("dm_sound_enabled").HasDefaultValue(true);
        builder.Property(e => e.RoomSoundLevel).HasColumnName("room_sound_level").HasDefaultValue("mentions_only");
        builder.Property(e => e.DndEnabled).HasColumnName("dnd_enabled").HasDefaultValue(false);
        builder.Property(e => e.BrowserNotificationsEnabled).HasColumnName("browser_notifications_enabled").HasDefaultValue(false);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
    }
}
