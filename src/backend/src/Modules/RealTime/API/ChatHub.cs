using MediatR;
using Messaging.Application.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealTime.Domain;
using Shared.Contracts.Interfaces;

namespace RealTime.API;

public sealed record SendMessageRequest(Guid MessageId, Guid RoomId, string Content);

[Authorize]
public sealed class ChatHub : Hub<IChatHubClient>
{
    private readonly ISender _sender;
    private readonly IPresenceService _presence;

    public ChatHub(ISender sender, IPresenceService presence)
    {
        _sender = sender;
        _presence = presence;
    }

    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var userId))
        {
            await _presence.SetOnlineAsync(userId);
            await Clients.All.PresenceUpdate(userId, isOnline: true);
        }

        var roomId = Context.GetHttpContext()?.Request.Query["roomId"].ToString();
        if (!string.IsNullOrWhiteSpace(roomId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var userId))
        {
            await _presence.SetOfflineAsync(userId);
            await Clients.All.PresenceUpdate(userId, isOnline: false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Clients call this every 15 seconds to keep the presence key alive (30s TTL)
    public async Task Heartbeat()
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var userId))
            await _presence.SetOnlineAsync(userId);
    }

    public Task JoinRoom(string roomId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");

    public async Task SendMessage(SendMessageRequest request)
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            throw new HubException("Unauthorized");

        var displayName = Context.User?.FindFirst("preferred_username")?.Value ?? "Unknown";
        var avatarUrl = Context.User?.FindFirst("picture")?.Value;

        try
        {
            await _sender.Send(new SendMessageCommand(
                MessageId: request.MessageId,
                RoomId: request.RoomId,
                UserId: userId,
                AuthorDisplayName: displayName,
                AuthorAvatarUrl: avatarUrl,
                Content: request.Content
            ));
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException("Unauthorized");
        }
    }
}
