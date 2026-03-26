using EnrichedMessaging.Domain;
using MediatR;

namespace EnrichedMessaging.Application.Queries;

public sealed record GetBookmarksQuery(Guid UserId) : IRequest<GetBookmarksResult>;

public sealed record GetBookmarksResult(
    IReadOnlyList<BookmarkFolder> Folders,
    IReadOnlyList<Bookmark> Unfiled
);

public sealed class GetBookmarksQueryHandler : IRequestHandler<GetBookmarksQuery, GetBookmarksResult>
{
    private readonly IBookmarkRepository _bookmarks;

    public GetBookmarksQueryHandler(IBookmarkRepository bookmarks)
    {
        _bookmarks = bookmarks;
    }

    public async Task<GetBookmarksResult> Handle(GetBookmarksQuery request, CancellationToken cancellationToken)
    {
        var (folders, bookmarks) = await _bookmarks.GetByUserAsync(request.UserId, cancellationToken);

        var bookmarkMap = bookmarks.ToLookup(b => b.FolderId);
        var unfiled = bookmarkMap[null].ToList();

        var foldersWithBookmarks = folders
            .Select(f => f with { Bookmarks = bookmarkMap[f.Id].ToList() })
            .ToList();

        return new GetBookmarksResult(foldersWithBookmarks, unfiled);
    }
}
