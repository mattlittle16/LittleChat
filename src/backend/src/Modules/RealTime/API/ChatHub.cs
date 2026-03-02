using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealTime.Domain;

namespace RealTime.API;

[Authorize]
public sealed class ChatHub : Hub<IChatHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var roomId = Context.GetHttpContext()?.Request.Query["roomId"].ToString();
        if (!string.IsNullOrWhiteSpace(roomId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
        await base.OnConnectedAsync();
    }
}
