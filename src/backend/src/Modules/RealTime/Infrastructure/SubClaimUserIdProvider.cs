using Microsoft.AspNetCore.SignalR;

namespace RealTime.Infrastructure;

public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirst("sub")?.Value;
}
