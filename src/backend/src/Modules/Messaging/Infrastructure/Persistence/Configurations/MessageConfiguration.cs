using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Messaging.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<MessageEntity>
{
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever(); // client-generated
        builder.Property(m => m.RoomId).HasColumnName("room_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.Property(m => m.IsSystem).HasColumnName("is_system").HasDefaultValue(false);
        builder.Property(m => m.Content).HasColumnName("content").IsRequired().HasMaxLength(4000);
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(m => m.EditedAt).HasColumnName("edited_at");
        builder.Property(m => m.ExpiresAt).HasColumnName("expires_at")
            .HasDefaultValueSql("NOW() + INTERVAL '30 days'");

        // Full-text search vector — GENERATED ALWAYS AS computed column
        builder.Property(m => m.SearchVector)
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql("to_tsvector('english', content)", stored: true);

        builder.HasOne(m => m.Room).WithMany(r => r.Messages).HasForeignKey(m => m.RoomId);
        builder.HasOne(m => m.User).WithMany(u => u.Messages).HasForeignKey(m => m.UserId)
            .IsRequired(false);

        // GIN index for full-text search
        builder.HasIndex(m => m.SearchVector)
            .HasMethod("GIN")
            .HasDatabaseName("messages_search_idx");

        // Composite index for history pagination queries
        builder.HasIndex(m => new { m.RoomId, m.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("messages_room_created_idx");
    }
}
