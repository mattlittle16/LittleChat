using Shared.Contracts.DTOs;

namespace Shared.Contracts.Events;

public sealed class MessageSentIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<AttachmentEventData> Attachments { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public bool IsSystem { get; init; }
    public string MessageType { get; init; } = "text";
    public PollDataDto? PollData { get; init; }
    public QuoteDto? QuoteData { get; init; }
}
