using EnrichedMessaging.Domain;
using MediatR;

namespace EnrichedMessaging.Application.Commands;

public sealed record CreateBookmarkFolderCommand(Guid UserId, string Name) : IRequest<BookmarkFolder>;

public sealed class CreateBookmarkFolderCommandHandler : IRequestHandler<CreateBookmarkFolderCommand, BookmarkFolder>
{
    private readonly IBookmarkRepository _bookmarks;

    public CreateBookmarkFolderCommandHandler(IBookmarkRepository bookmarks)
    {
        _bookmarks = bookmarks;
    }

    public async Task<BookmarkFolder> Handle(CreateBookmarkFolderCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
            throw new InvalidOperationException("Folder name must be 1–100 characters.");

        var folder = await _bookmarks.CreateFolderAsync(request.UserId, request.Name, cancellationToken);
        return folder!;
    }
}
