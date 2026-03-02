using MediatR;
using Messaging.Application.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealTime.Domain;

namespace RealTime.API;

public sealed record SendMessageRequest(Guid MessageId, Guid RoomId, string Content);

[Authorize]
public sealed class ChatHub : Hub<IChatHubClient>
{
    private readonly ISender _sender;

    public ChatHub(ISender sender)
    {
        _sender = sender;
    }

    public override async Task OnConnectedAsync()
    {
        var roomId = Context.GetHttpContext()?.Request.Query["roomId"].ToString();
        if (!string.IsNullOrWhiteSpace(roomId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
        await base.OnConnectedAsync();
    }

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
