using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnrichedMessaging.Infrastructure.Configurations;

public sealed class BookmarkFolderConfiguration : IEntityTypeConfiguration<BookmarkFolderEntity>
{
    public void Configure(EntityTypeBuilder<BookmarkFolderEntity> builder)
    {
        builder.ToTable("bookmark_folders");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasMany(f => f.Bookmarks)
            .WithOne(b => b.Folder)
            .HasForeignKey(b => b.FolderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
