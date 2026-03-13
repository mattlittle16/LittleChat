using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.ExternalId).HasColumnName("external_id").IsRequired();
        builder.HasIndex(u => u.ExternalId).IsUnique().HasDatabaseName("IX_users_external_id");
        builder.Property(u => u.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder.Property(u => u.ProfileImagePath).HasColumnName("profile_image_path");
        builder.Property(u => u.CropX).HasColumnName("crop_x");
        builder.Property(u => u.CropY).HasColumnName("crop_y");
        builder.Property(u => u.CropZoom).HasColumnName("crop_zoom");
    }
}
