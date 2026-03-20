using Microsoft.AspNetCore.SignalR;
using RealTime.Domain;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.API;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<ChatHub, IChatHubClient> _hubContext;
    private readonly IHubContext<ChatHub> _untypedHubContext;

    public SignalRRealtimeNotifier(IHubContext<ChatHub, IChatHubClient> hubContext, IHubContext<ChatHub> untypedHubContext)
    {
        _hubContext = hubContext;
        _untypedHubContext = untypedHubContext;
    }

    public Task BroadcastToRoomAsync(string roomId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        var group = _hubContext.Clients.Group($"room:{roomId}");
        return eventName switch
        {
            "ReceiveMessage" => group.ReceiveMessage((MessageDto)payload),
            "MessageEdited" when payload is MessageEditedIntegrationEvent me =>
                group.MessageEdited(me.MessageId, me.RoomId, me.Content, me.EditedAt),
            "MessageDeleted" when payload is MessageDeletedIntegrationEvent md =>
                group.MessageDeleted(md.MessageId, md.RoomId),
            "ReactionUpdated" when payload is ReactionUpdatedIntegrationEvent e =>
                group.ReactionUpdated(e.MessageId, e.RoomId, e.Emoji, e.Count, e.Added, e.Users),
            "DmDeleted" when payload is DmDeletedIntegrationEvent d =>
                group.DmDeleted(d.RoomId),
            "RoomDeleted" when payload is RoomDeletedIntegrationEvent r =>
                group.RoomDeleted(r.RoomId),
            "MemberListChanged" when payload is Guid mlcRoomId =>
                group.MemberListChanged(mlcRoomId),
            _ => Task.CompletedTask,
        };
    }

    public Task BroadcastToAllAsync(string eventName, object payload, CancellationToken ct = default)
        => _untypedHubContext.Clients.All.SendAsync(eventName, payload, ct);

    public Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        var user = _hubContext.Clients.User(userId);
        return eventName switch
        {
            "MentionNotification" when payload is MentionDetectedIntegrationEvent e =>
                user.MentionNotification(e.MessageId, e.RoomId, e.RoomName, e.FromUserId, e.FromDisplayName, e.ContentPreview),
            "NotificationReceived" when payload is NotificationDto n =>
                user.NotificationReceived(n),
            "DmCreated" when payload is DmCreatedIntegrationEvent c =>
                user.DmCreated(c.RoomId),
            "RoomMembershipChanged" when payload is MemberAddedIntegrationEvent a =>
                user.RoomMembershipChanged(a.RoomId, a.AddedUserId, "added"),
            "RemovedFromRoom" when payload is MemberRemovedIntegrationEvent r =>
                user.RemovedFromRoom(r.RoomId),
            "ForceLogout" =>
                user.ForceLogout(),
            _ => Task.CompletedTask,
        };
    }
}
