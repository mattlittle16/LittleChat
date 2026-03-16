namespace Reactions.Application;

public interface IReactionRepository
{
    /// <summary>
    /// Toggles an emoji reaction for a user on a message.
    /// Returns (Added: true if inserted, false if deleted, Count, DisplayNames of all reactors for this emoji).
    /// </summary>
    Task<(bool Added, int Count, IReadOnlyList<string> Users)> ToggleAsync(
        Guid messageId,
        Guid userId,
        string emoji,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the author user ID, message content, and room name for a message.
    /// Returns (AuthorUserId: Guid.Empty, MessageContent: "", RoomName: "") when the message does not exist.
    /// </summary>
    Task<(Guid AuthorUserId, string MessageContent, string RoomName)> GetMessageInfoAsync(
        Guid messageId,
        CancellationToken ct = default);
}
