namespace Shared.Contracts.Interfaces;

public interface IPresenceService
{
    Task SetOnlineAsync(Guid userId, string connectionId, CancellationToken ct = default);
    /// <summary>Removes this connection. Returns true if the user is now fully offline (no remaining connections).</summary>
    Task<bool> SetOfflineAsync(Guid userId, string connectionId, CancellationToken ct = default);
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAllOnlineAsync(CancellationToken ct = default);
}
