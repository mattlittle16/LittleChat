using API.Services;
using MediatR;
using Messaging.Application.Commands;
using Messaging.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Reactions.Application.Commands;
using RealTime.Domain;
using Shared.Contracts;
using Shared.Contracts.Interfaces;

namespace API;

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
    private readonly SignalRRateLimiter _rateLimiter;

    public ChatHub(ISender sender, IPresenceService presence, SignalRRateLimiter rateLimiter)
    {
        _sender = sender;
        _presence = presence;
        _rateLimiter = rateLimiter;
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

    // Heartbeat keeps presence alive every 15s so that stale Redis state (e.g. after a Valkey
    // eviction or missed OnConnectedAsync) self-heals without a page reload. Uses SetOnlineAsync
    // (additive SADD) rather than ReassertAsync (destructive DEL+SADD) so that a heartbeat from
    // one connection never evicts sibling connections belonging to the same user logged in on
    // multiple browsers simultaneously.
    public async Task Heartbeat()
    {
        var userId = Context.User?.GetInternalUserId();
        if (userId is null) return;
        await _presence.SetOnlineAsync(userId.Value, Context.ConnectionId);
    }

    // Called by clients on SignalR reconnect. Uses SetOnlineAsync (additive) so that a reconnect
    // on one browser does not evict presence entries for the same user's other open sessions.
    // OnConnectedAsync already registers the new connectionId; this is a safety net for cases
    // where OnConnectedAsync was missed (e.g. server crash mid-session).
    public async Task ReassertPresence()
    {
        var userId = Context.User?.GetInternalUserId();
        if (userId is null) return;
        await _presence.SetOnlineAsync(userId.Value, Context.ConnectionId);
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

        if (request.Content.Length > 15000)
            throw new HubException("Message content exceeds the maximum length of 15,000 characters.");

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

        if (request.Content.Length > 15000)
            throw new HubException("Message content exceeds the maximum length of 15,000 characters.");

        if (!_rateLimiter.IsAllowed(userId.ToString()))
            throw new HubException("Too many messages. Please slow down.");

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
