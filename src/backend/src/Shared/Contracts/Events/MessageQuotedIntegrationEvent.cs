namespace Shared.Contracts.Events;

public sealed class MessageQuotedIntegrationEvent : IntegrationEvent
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public Guid QuotedMessageAuthorId { get; init; }
    public Guid QuoterUserId { get; init; }
    public string QuoterDisplayName { get; init; } = string.Empty;
    public string ContentPreview { get; init; } = string.Empty;
}
