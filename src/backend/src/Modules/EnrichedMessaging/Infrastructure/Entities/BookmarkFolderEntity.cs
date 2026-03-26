namespace EnrichedMessaging.Infrastructure.Entities;

public sealed class BookmarkFolderEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<BookmarkEntity> Bookmarks { get; set; } = [];
}
