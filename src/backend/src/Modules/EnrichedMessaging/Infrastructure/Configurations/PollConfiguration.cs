using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnrichedMessaging.Infrastructure.Configurations;

public sealed class PollConfiguration : IEntityTypeConfiguration<PollEntity>
{
    public void Configure(EntityTypeBuilder<PollEntity> builder)
    {
        builder.ToTable("polls");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(p => p.Question).HasColumnName("question").IsRequired();
        builder.Property(p => p.VoteMode).HasColumnName("vote_mode").IsRequired().HasMaxLength(10);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(p => p.MessageId).IsUnique().HasDatabaseName("polls_message_id_idx");

        builder.HasMany(p => p.Options)
            .WithOne(o => o.Poll)
            .HasForeignKey(o => o.PollId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
