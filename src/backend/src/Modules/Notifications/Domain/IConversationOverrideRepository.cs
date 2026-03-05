namespace Notifications.Domain;

public interface IConversationOverrideRepository
{
    Task<IReadOnlyList<ConversationNotificationOverride>> GetAllForUserAsync(Guid userId, CancellationToken ct = default);
    Task UpsertAsync(ConversationNotificationOverride @override, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid roomId, CancellationToken ct = default);
}
