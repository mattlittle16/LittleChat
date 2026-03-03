namespace Shared.Contracts.Interfaces;

public interface IPresenceService
{
    Task SetOnlineAsync(Guid userId, CancellationToken ct = default);
    Task SetOfflineAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAllOnlineAsync(CancellationToken ct = default);
}
