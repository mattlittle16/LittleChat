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
}
