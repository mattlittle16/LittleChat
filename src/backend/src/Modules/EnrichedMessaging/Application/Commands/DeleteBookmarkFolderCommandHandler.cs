using EnrichedMessaging.Domain;
using MediatR;

namespace EnrichedMessaging.Application.Commands;

public sealed record DeleteBookmarkFolderCommand(Guid FolderId, Guid UserId) : IRequest;

public sealed class DeleteBookmarkFolderCommandHandler : IRequestHandler<DeleteBookmarkFolderCommand>
{
    private readonly IBookmarkRepository _bookmarks;

    public DeleteBookmarkFolderCommandHandler(IBookmarkRepository bookmarks)
    {
        _bookmarks = bookmarks;
    }

    public async Task Handle(DeleteBookmarkFolderCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _bookmarks.DeleteFolderAsync(request.FolderId, request.UserId, cancellationToken);
        if (!deleted)
            throw new InvalidOperationException("Folder not found or not owned by caller.");
    }
}
