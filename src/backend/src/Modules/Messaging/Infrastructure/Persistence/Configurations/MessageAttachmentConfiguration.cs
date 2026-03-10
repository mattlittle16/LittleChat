using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Infrastructure.Persistence.Configurations;

public sealed class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachmentEntity>
{
    public void Configure(EntityTypeBuilder<MessageAttachmentEntity> builder)
    {
        builder.ToTable("message_attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.MessageId).HasColumnName("message_id");
        builder.Property(a => a.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(500);
        builder.Property(a => a.FileSize).HasColumnName("file_size");
        builder.Property(a => a.FilePath).HasColumnName("file_path").IsRequired().HasMaxLength(1000);
        builder.Property(a => a.ContentType).HasColumnName("content_type").IsRequired().HasMaxLength(200);
        builder.Property(a => a.IsImage).HasColumnName("is_image");
        builder.Property(a => a.DisplayOrder).HasColumnName("display_order");

        builder.HasOne(a => a.Message)
               .WithMany(m => m.Attachments)
               .HasForeignKey(a => a.MessageId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.MessageId)
               .HasDatabaseName("ix_message_attachments_message_id");
    }
}
