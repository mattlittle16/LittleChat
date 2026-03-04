using Shared.Contracts.DTOs;

namespace RealTime.Domain;

public interface IChatHubClient
{
    Task ReceiveMessage(MessageDto message);
    Task MessageEdited(Guid messageId, Guid roomId, string content, DateTime editedAt);
    Task MessageDeleted(Guid messageId, Guid roomId);
    Task ReactionUpdated(Guid messageId, Guid roomId, string emoji, int count, bool added, IReadOnlyList<string> users);
    Task UserTyping(Guid roomId, Guid userId, string displayName);
    Task PresenceUpdate(Guid userId, bool isOnline);
    Task PresenceSnapshot(IReadOnlyList<Guid> onlineUserIds);
    Task MentionNotification(Guid messageId, Guid roomId, string roomName, Guid fromUserId, string fromDisplayName, string contentPreview);
    Task DmCreated(Guid roomId);
    Task DmDeleted(Guid roomId);
    Task RoomDeleted(Guid roomId);
}
