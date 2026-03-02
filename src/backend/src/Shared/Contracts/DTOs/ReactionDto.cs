namespace Shared.Contracts.DTOs;

public record ReactionDto(
    string Emoji,
    int Count,
    IReadOnlyList<string> Users  // display names
);
