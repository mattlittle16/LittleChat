namespace Shared.Contracts.Events;

public sealed class MentionDetectedIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public Guid MentionedUserId { get; init; }
    public Guid FromUserId { get; init; }
    public string FromDisplayName { get; init; } = string.Empty;
    public string ContentPreview { get; init; } = string.Empty;
}
