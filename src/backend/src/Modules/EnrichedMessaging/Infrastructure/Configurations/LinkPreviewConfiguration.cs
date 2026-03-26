using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnrichedMessaging.Infrastructure.Configurations;

public sealed class LinkPreviewConfiguration : IEntityTypeConfiguration<LinkPreviewEntity>
{
    public void Configure(EntityTypeBuilder<LinkPreviewEntity> builder)
    {
        builder.ToTable("link_previews");
        builder.HasKey(lp => lp.Id);
        builder.Property(lp => lp.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(lp => lp.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(lp => lp.Url).HasColumnName("url").IsRequired();
        builder.Property(lp => lp.Title).HasColumnName("title");
        builder.Property(lp => lp.Description).HasColumnName("description");
        builder.Property(lp => lp.ThumbnailUrl).HasColumnName("thumbnail_url");
        builder.Property(lp => lp.IsDismissed).HasColumnName("is_dismissed").HasDefaultValue(false);
        builder.Property(lp => lp.FetchedAt).HasColumnName("fetched_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(lp => lp.MessageId)
            .IsUnique()
            .HasDatabaseName("link_previews_message_id_idx");
    }
}
