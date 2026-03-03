namespace Identity.Domain;

public interface IUserRepository
{
    /// <summary>
    /// Upserts a user by external identity provider ID.
    /// Returns the internal user ID and whether the user was newly created.
    /// </summary>
    Task<(bool IsNew, Guid UserId)> UpsertAsync(string externalId, string displayName, string? avatarUrl, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(string? nameFilter, CancellationToken cancellationToken = default);
}
