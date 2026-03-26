namespace Shared.Contracts.DTOs;

public record QuoteDto(
    Guid? OriginalMessageId,
    string AuthorDisplayName,
    string ContentSnapshot,
    bool OriginalAvailable
);
