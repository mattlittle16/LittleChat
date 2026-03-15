using MediatR;
using Messaging.Application.Commands;
using Messaging.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Reactions.Application.Commands;
using RealTime.Domain;
using Shared.Contracts;
using Shared.Contracts.Interfaces;

namespace RealTime.API;

public sealed record SendMessageRequest(Guid MessageId, Guid RoomId, string Content);
public sealed record AddReactionRequest(Guid MessageId, Guid RoomId, string Emoji);
public sealed record StartTypingRequest(Guid RoomId);
public sealed record EditMessageRequest(Guid MessageId, Guid RoomId, string Content);
public sealed record DeleteMessageRequest(Guid MessageId, Guid RoomId);

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
        var userId = Context.User?.GetInternalUserId()
            ?? throw new HubException("Unauthorized");

        await _presence.SetOnlineAsync(userId, Context.ConnectionId);

        // Join ALL room groups for this user so real-time events arrive from every room,
        // not only the currently active one (fixes DM notification bug).
        var allRoomIds = await _sender.Send(new GetUserRoomIdsQuery(userId));
        foreach (var id in allRoomIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{id}");

        // Scope presence broadcast to users who share at least one room — not Clients.All.
        // Recipients may receive this more than once if they share multiple rooms; the
        // frontend PresenceUpdate handler is idempotent so duplicate deliveries are safe.
        foreach (var id in allRoomIds)
            await Clients.Group($"room:{id}").PresenceUpdate(userId, isOnline: true);

        // Send the newly connected client a snapshot of all currently online users
        // so their presence store is populated correctly on (re)connect.
        var onlineUserIds = await _presence.GetAllOnlineAsync();
        await Clients.Caller.PresenceSnapshot(onlineUserIds);

        var roomId = Context.GetHttpContext()?.Request.Query["roomId"].ToString();
        if (!string.IsNullOrWhiteSpace(roomId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.GetInternalUserId();
        if (userId is not null)
        {
            var nowFullyOffline = await _presence.SetOfflineAsync(userId.Value, Context.ConnectionId);
            if (nowFullyOffline)
            {
                // Scope disconnect broadcast to shared rooms, same as connect.
                var allRoomIds = await _sender.Send(new GetUserRoomIdsQuery(userId.Value));
                foreach (var id in allRoomIds)
                    await Clients.Group($"room:{id}").PresenceUpdate(userId.Value, isOnline: false);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Heartbeat is kept for backwards compatibility with existing clients.
    // The new refcount model does not require TTL refresh — this is now a no-op.
    public Task Heartbeat() => Task.CompletedTask;

    // Called by clients on SignalR reconnect to fix stale presence refcounts left by a server crash.
    // Forces this user's refcount to 1 so a single subsequent disconnect correctly broadcasts offline.
    // Safe to call multiple times — idempotent on an already-healthy connection.
    public async Task ReassertPresence()
    {
        var userId = Context.User?.GetInternalUserId();
        if (userId is null) return;
        await _presence.ReassertAsync(userId.Value);
    }

    public Task JoinRoom(string roomId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");

    public Task LeaveRoom(string roomId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomId}");

    public async Task<(bool Added, int Count)> AddReaction(AddReactionRequest request)
    {
        var userId = Context.User?.GetInternalUserId()
            ?? throw new HubException("Unauthorized");

        var displayName = Context.User?.FindFirst("preferred_username")?.Value ?? "Unknown";

        return await _sender.Send(new AddReactionCommand(
            MessageId: request.MessageId,
            RoomId: request.RoomId,
            UserId: userId,
            DisplayName: displayName,
            Emoji: request.Emoji));
    }

    public async Task EditMessage(EditMessageRequest request)
    {
        var userId = Context.User?.GetInternalUserId()
            ?? throw new HubException("Unauthorized");

        try
        {
            await _sender.Send(new EditMessageCommand(
                request.MessageId, request.RoomId, userId, request.Content));
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException("You can only edit your own messages.");
        }
    }

    public async Task DeleteMessage(DeleteMessageRequest request)
    {
        var userId = Context.User?.GetInternalUserId()
            ?? throw new HubException("Unauthorized");

        try
        {
            await _sender.Send(new DeleteMessageCommand(
                request.MessageId, request.RoomId, userId));
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException("You can only delete your own messages.");
        }
    }

    public async Task StartTyping(StartTypingRequest request)
    {
        var userId = Context.User?.GetInternalUserId();
        if (userId is null) return;

        var displayName = Context.User?.FindFirst("preferred_username")?.Value ?? "Unknown";

        // Broadcast to all others in the room (excluding the sender)
        await Clients.OthersInGroup($"room:{request.RoomId}")
            .UserTyping(request.RoomId, userId.Value, displayName);
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        var userId = Context.User?.GetInternalUserId()
            ?? throw new HubException("Unauthorized");

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
                Content: request.Content,
                Files: []
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
