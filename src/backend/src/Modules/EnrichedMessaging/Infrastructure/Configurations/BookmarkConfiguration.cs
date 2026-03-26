using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnrichedMessaging.Infrastructure.Configurations;

public sealed class BookmarkConfiguration : IEntityTypeConfiguration<BookmarkEntity>
{
    public void Configure(EntityTypeBuilder<BookmarkEntity> builder)
    {
        builder.ToTable("message_bookmarks");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(b => b.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(b => b.FolderId).HasColumnName("folder_id");
        builder.Property(b => b.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(b => b.RoomName).HasColumnName("room_name").IsRequired();
        builder.Property(b => b.AuthorDisplayName).HasColumnName("author_display_name").IsRequired();
        builder.Property(b => b.ContentPreview).HasColumnName("content_preview").IsRequired();
        builder.Property(b => b.MessageCreatedAt).HasColumnName("message_created_at").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(b => new { b.UserId, b.MessageId })
            .IsUnique()
            .HasDatabaseName("message_bookmarks_user_message_idx");
    }
}
