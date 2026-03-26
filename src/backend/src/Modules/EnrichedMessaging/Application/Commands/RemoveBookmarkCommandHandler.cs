using EnrichedMessaging.Domain;
using MediatR;

namespace EnrichedMessaging.Application.Commands;

public sealed record RemoveBookmarkCommand(Guid BookmarkId, Guid UserId) : IRequest;

public sealed class RemoveBookmarkCommandHandler : IRequestHandler<RemoveBookmarkCommand>
{
    private readonly IBookmarkRepository _bookmarks;

    public RemoveBookmarkCommandHandler(IBookmarkRepository bookmarks)
    {
        _bookmarks = bookmarks;
    }

    public async Task Handle(RemoveBookmarkCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _bookmarks.DeleteAsync(request.BookmarkId, request.UserId, cancellationToken);
        if (!deleted)
            throw new InvalidOperationException("Bookmark not found or not owned by caller.");
    }
}
