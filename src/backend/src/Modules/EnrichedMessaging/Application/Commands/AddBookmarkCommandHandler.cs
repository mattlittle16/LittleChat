using EnrichedMessaging.Domain;
using MediatR;

namespace EnrichedMessaging.Application.Commands;

public sealed record AddBookmarkCommand(Guid UserId, Guid MessageId, Guid? FolderId) : IRequest<Bookmark?>;

public sealed class AddBookmarkCommandHandler : IRequestHandler<AddBookmarkCommand, Bookmark?>
{
    private readonly IBookmarkRepository _bookmarks;

    public AddBookmarkCommandHandler(IBookmarkRepository bookmarks)
    {
        _bookmarks = bookmarks;
    }

    public async Task<Bookmark?> Handle(AddBookmarkCommand request, CancellationToken cancellationToken)
    {
        if (request.FolderId.HasValue)
        {
            var folderExists = await _bookmarks.FolderExistsAsync(request.FolderId.Value, request.UserId, cancellationToken);
            if (!folderExists)
                throw new InvalidOperationException("Folder not found or not owned by user.");
        }

        var bookmark = await _bookmarks.AddAsync(request.UserId, request.MessageId, request.FolderId, cancellationToken);
        if (bookmark is null)
            throw new InvalidOperationException("Message not found.");

        return bookmark;
    }
}
