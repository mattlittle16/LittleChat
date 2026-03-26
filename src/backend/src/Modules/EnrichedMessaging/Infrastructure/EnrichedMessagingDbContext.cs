using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnrichedMessaging.Infrastructure;

public sealed class EnrichedMessagingDbContext : DbContext
{
    public EnrichedMessagingDbContext(DbContextOptions<EnrichedMessagingDbContext> options) : base(options) { }

    public DbSet<PollEntity> Polls => Set<PollEntity>();
    public DbSet<PollOptionEntity> PollOptions => Set<PollOptionEntity>();
    public DbSet<PollVoteEntity> PollVotes => Set<PollVoteEntity>();
    public DbSet<HighlightEntity> MessageHighlights => Set<HighlightEntity>();
    public DbSet<BookmarkFolderEntity> BookmarkFolders => Set<BookmarkFolderEntity>();
    public DbSet<BookmarkEntity> MessageBookmarks => Set<BookmarkEntity>();
    public DbSet<LinkPreviewEntity> LinkPreviews => Set<LinkPreviewEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnrichedMessagingDbContext).Assembly);
    }
}
