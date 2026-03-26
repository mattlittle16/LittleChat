namespace EnrichedMessaging.Domain;

public interface IBookmarkRepository
{
    Task<(IReadOnlyList<BookmarkFolder> Folders, IReadOnlyList<Bookmark> Bookmarks)> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Bookmark?> AddAsync(Guid userId, Guid messageId, Guid? folderId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid bookmarkId, Guid userId, CancellationToken ct = default);
    Task<bool> UpdateFolderAsync(Guid bookmarkId, Guid userId, Guid? folderId, CancellationToken ct = default);
    Task<BookmarkFolder?> CreateFolderAsync(Guid userId, string name, CancellationToken ct = default);
    Task<bool> DeleteFolderAsync(Guid folderId, Guid userId, CancellationToken ct = default);
    Task<bool> FolderExistsAsync(Guid folderId, Guid userId, CancellationToken ct = default);
}
