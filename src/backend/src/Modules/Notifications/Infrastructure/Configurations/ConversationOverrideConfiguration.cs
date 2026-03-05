using Notifications.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Notifications.Infrastructure.Configurations;

public sealed class ConversationOverrideConfiguration : IEntityTypeConfiguration<ConversationOverrideEntity>
{
    public void Configure(EntityTypeBuilder<ConversationOverrideEntity> builder)
    {
        builder.ToTable("conversation_notification_overrides");
        builder.HasKey(e => new { e.UserId, e.RoomId });
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.RoomId).HasColumnName("room_id");
        builder.Property(e => e.Level).HasColumnName("level");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
    }
}
