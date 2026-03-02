using Microsoft.AspNetCore.SignalR;
using RealTime.Domain;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.API;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<ChatHub, IChatHubClient> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<ChatHub, IChatHubClient> hubContext)
    {
        _hubContext = hubContext;
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
            _ => Task.CompletedTask,
        };
    }

    public Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        // Used for user-targeted events (MentionNotification etc.)
        return Task.CompletedTask;
    }
}
