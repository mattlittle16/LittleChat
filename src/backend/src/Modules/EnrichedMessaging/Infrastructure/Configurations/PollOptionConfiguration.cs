using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnrichedMessaging.Infrastructure.Configurations;

public sealed class PollOptionConfiguration : IEntityTypeConfiguration<PollOptionEntity>
{
    public void Configure(EntityTypeBuilder<PollOptionEntity> builder)
    {
        builder.ToTable("poll_options");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.PollId).HasColumnName("poll_id").IsRequired();
        builder.Property(o => o.Text).HasColumnName("text").IsRequired();
        builder.Property(o => o.DisplayOrder).HasColumnName("display_order").IsRequired();

        builder.HasMany(o => o.Votes)
            .WithOne(v => v.Option)
            .HasForeignKey(v => v.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
