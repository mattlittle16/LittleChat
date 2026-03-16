namespace Shared.Contracts.Events;

public sealed class TopicAlertIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public Guid SenderUserId { get; init; }
    public string SenderDisplayName { get; init; } = string.Empty;
    public string ContentPreview { get; init; } = string.Empty;
    public IReadOnlyList<Guid> RecipientUserIds { get; init; } = [];
}
