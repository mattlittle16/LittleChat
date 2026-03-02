namespace Shared.Contracts.Interfaces;

/// <summary>Cross-module interface for looking up user IDs by display name, without depending on Identity.Domain.</summary>
public interface IUserLookupService
{
    Task<Guid?> FindIdByDisplayNameAsync(string displayName, CancellationToken ct = default);
}
