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
}
