using Microsoft.AspNetCore.SignalR;
using RealTime.Domain;
using Shared.Contracts.DTOs;
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
            "MessageEdited" when payload is (Guid msgId, Guid rId, string content, DateTime editedAt) =>
                group.MessageEdited(msgId, rId, content, editedAt),
            "MessageDeleted" when payload is (Guid dMsgId, Guid dRId) =>
                group.MessageDeleted(dMsgId, dRId),
            _ => Task.CompletedTask,
        };
    }

    public Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        // Used for user-targeted events (MentionNotification etc.)
        return Task.CompletedTask;
    }
}
