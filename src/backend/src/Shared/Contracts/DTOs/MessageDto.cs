namespace Shared.Contracts.DTOs;

public record MessageDto(
    Guid Id,
    Guid RoomId,
    AuthorDto Author,
    string Content,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<ReactionDto> Reactions,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsSystem = false,
    string MessageType = "text",
    QuoteDto? Quote = null,
    PollDataDto? Poll = null,
    LinkPreviewDataDto? LinkPreview = null
);
