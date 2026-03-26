using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnrichedMessaging.Infrastructure.Configurations;

public sealed class HighlightConfiguration : IEntityTypeConfiguration<HighlightEntity>
{
    public void Configure(EntityTypeBuilder<HighlightEntity> builder)
    {
        builder.ToTable("message_highlights");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(h => h.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(h => h.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(h => h.HighlightedByUserId).HasColumnName("highlighted_by_user_id").IsRequired();
        builder.Property(h => h.HighlightedByDisplayName).HasColumnName("highlighted_by_display_name").IsRequired();
        builder.Property(h => h.HighlightedAt).HasColumnName("highlighted_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(h => new { h.RoomId, h.MessageId })
            .IsUnique()
            .HasDatabaseName("message_highlights_room_message_idx");
    }
}
