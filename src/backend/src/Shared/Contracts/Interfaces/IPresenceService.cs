namespace Shared.Contracts.Interfaces;

public interface IPresenceService
{
    Task SetOnlineAsync(Guid userId, string connectionId, CancellationToken ct = default);
    /// <summary>Removes this connection. Returns true if the user is now fully offline (no remaining connections).</summary>
    Task<bool> SetOfflineAsync(Guid userId, string connectionId, CancellationToken ct = default);
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAllOnlineAsync(CancellationToken ct = default);
    /// <summary>Clears all presence state. Call on startup to remove stale entries from a previous run or crash.</summary>
    Task ClearAllAsync(CancellationToken ct = default);
    /// <summary>Resets a user's active connection set to contain only this connection and ensures
    /// they are in the online set. Called by clients on reconnect to recover from stale state
    /// left by a server crash.</summary>
    Task ReassertAsync(Guid userId, string connectionId, CancellationToken ct = default);
}
