using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Infrastructure.Persistence.Configurations;

public sealed class ReactionConfiguration : IEntityTypeConfiguration<ReactionEntity>
{
    public void Configure(EntityTypeBuilder<ReactionEntity> builder)
    {
        builder.ToTable("reactions");
        // Composite PK: (message_id, user_id, emoji) — enforces one-per-emoji-per-user-per-message
        builder.HasKey(r => new { r.MessageId, r.UserId, r.Emoji });
        builder.Property(r => r.MessageId).HasColumnName("message_id");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.Emoji).HasColumnName("emoji").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        // ON DELETE CASCADE — reactions deleted when parent message is deleted
        builder.HasOne(r => r.Message)
            .WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
