using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnrichedMessaging.Infrastructure.Configurations;

public sealed class PollVoteConfiguration : IEntityTypeConfiguration<PollVoteEntity>
{
    public void Configure(EntityTypeBuilder<PollVoteEntity> builder)
    {
        builder.ToTable("poll_votes");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.PollId).HasColumnName("poll_id").IsRequired();
        builder.Property(v => v.OptionId).HasColumnName("option_id").IsRequired();
        builder.Property(v => v.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(v => v.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(v => new { v.PollId, v.OptionId, v.UserId })
            .IsUnique()
            .HasDatabaseName("poll_votes_unique_idx");
    }
}
