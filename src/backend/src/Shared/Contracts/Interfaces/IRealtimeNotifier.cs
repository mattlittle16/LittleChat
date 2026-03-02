namespace Shared.Contracts.Interfaces;

public interface IRealtimeNotifier
{
    Task BroadcastToRoomAsync(string roomId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken cancellationToken = default);
}
