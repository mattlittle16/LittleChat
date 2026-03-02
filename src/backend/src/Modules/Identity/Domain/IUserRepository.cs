namespace Identity.Domain;

public interface IUserRepository
{
    /// <summary>Upserts a user. Returns true if the user was newly created.</summary>
    Task<bool> UpsertAsync(Guid id, string displayName, string? avatarUrl, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
