namespace LittleChat.Modules.Admin.Application;

public interface ITokenBlocklistService
{
    Task BlockUserAsync(Guid internalUserId, TimeSpan banDuration, CancellationToken ct = default);
    Task<DateTimeOffset?> GetBanExpiryAsync(Guid internalUserId, CancellationToken ct = default);
    Task UnblockUserAsync(Guid internalUserId, CancellationToken ct = default);
}
