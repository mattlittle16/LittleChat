namespace Shared.Contracts.Events;

public sealed class MessageSentIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public long? FileSize { get; init; }
    public DateTime CreatedAt { get; init; }
}
