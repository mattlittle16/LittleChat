using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Infrastructure.Persistence;

public sealed class LittleChatDbContext : DbContext
{
    public LittleChatDbContext(DbContextOptions<LittleChatDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RoomEntity> Rooms => Set<RoomEntity>();
    public DbSet<RoomMembershipEntity> RoomMemberships => Set<RoomMembershipEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<ReactionEntity> Reactions => Set<ReactionEntity>();
    public DbSet<MessageAttachmentEntity> MessageAttachments => Set<MessageAttachmentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LittleChatDbContext).Assembly);
    }
}
