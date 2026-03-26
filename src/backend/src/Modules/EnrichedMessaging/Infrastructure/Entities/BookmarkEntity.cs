namespace EnrichedMessaging.Infrastructure.Entities;

public sealed class BookmarkEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MessageId { get; set; }
    public Guid? FolderId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public DateTime MessageCreatedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public BookmarkFolderEntity? Folder { get; set; }
}
