using MediatR;
using Messaging.Application.Commands;
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
        var userId = Context.User?.GetInternalUserId();
        if (userId is not null)
        {
            await _presence.SetOnlineAsync(userId.Value);
            await Clients.All.PresenceUpdate(userId.Value, isOnline: true);
        }

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
            await _presence.SetOfflineAsync(userId.Value);
            await Clients.All.PresenceUpdate(userId.Value, isOnline: false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Clients call this every 15 seconds to keep the presence key alive (30s TTL)
    public async Task Heartbeat()
    {
        var userId = Context.User?.GetInternalUserId();
        if (userId is not null)
            await _presence.SetOnlineAsync(userId.Value);
    }

    public Task JoinRoom(string roomId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");

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
