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
    /// <summary>Forces a user's refcount to exactly 1 and ensures they are in the online set.
    /// Called by clients on reconnect to recover from stale refcounts left by a server crash.</summary>
    Task ReassertAsync(Guid userId, CancellationToken ct = default);
}
