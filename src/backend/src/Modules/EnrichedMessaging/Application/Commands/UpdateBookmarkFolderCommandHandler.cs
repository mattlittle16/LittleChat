using EnrichedMessaging.Domain;
using MediatR;

namespace EnrichedMessaging.Application.Commands;

public sealed record UpdateBookmarkFolderCommand(Guid BookmarkId, Guid UserId, Guid? FolderId) : IRequest;

public sealed class UpdateBookmarkFolderCommandHandler : IRequestHandler<UpdateBookmarkFolderCommand>
{
    private readonly IBookmarkRepository _bookmarks;

    public UpdateBookmarkFolderCommandHandler(IBookmarkRepository bookmarks)
    {
        _bookmarks = bookmarks;
    }

    public async Task Handle(UpdateBookmarkFolderCommand request, CancellationToken cancellationToken)
    {
        if (request.FolderId.HasValue)
        {
            var folderExists = await _bookmarks.FolderExistsAsync(request.FolderId.Value, request.UserId, cancellationToken);
            if (!folderExists)
                throw new InvalidOperationException("Folder not found or not owned by user.");
        }

        var updated = await _bookmarks.UpdateFolderAsync(request.BookmarkId, request.UserId, request.FolderId, cancellationToken);
        if (!updated)
            throw new InvalidOperationException("Bookmark not found or not owned by caller.");
    }
}
