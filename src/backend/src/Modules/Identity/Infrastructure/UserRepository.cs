using Identity.Domain;
using Npgsql;
using Shared.Contracts.Interfaces;

namespace Identity.Infrastructure;

public sealed class UserRepository : IUserRepository, IUserLookupService
{
    private readonly NpgsqlDataSource _dataSource;

    public UserRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> UpsertAsync(Guid id, string displayName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        // Returns true (xmax=0) if a new row was inserted, false if an existing row was updated.
        const string sql = """
            INSERT INTO users (id, display_name, avatar_url, created_at)
            VALUES ($1, $2, $3, NOW())
            ON CONFLICT (id) DO UPDATE
                SET display_name = EXCLUDED.display_name,
                    avatar_url   = EXCLUDED.avatar_url
            RETURNING (xmax = 0) AS is_new
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(displayName);
        cmd.Parameters.AddWithValue(avatarUrl as object ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(string? nameFilter, CancellationToken cancellationToken = default)
    {
        // Both SQL strings are compile-time constants; user input is always bound as a parameter ($1),
        // never interpolated into SQL — no injection risk.
        const string allSql      = "SELECT id, display_name, avatar_url, created_at FROM users ORDER BY display_name";
        const string filteredSql = "SELECT id, display_name, avatar_url, created_at FROM users WHERE display_name ILIKE $1 ORDER BY display_name";

        var hasFilter = !string.IsNullOrWhiteSpace(nameFilter);
        await using var cmd = _dataSource.CreateCommand(hasFilter ? filteredSql : allSql);
        if (hasFilter)
        {
            // Wrap with LIKE wildcards here (not in SQL) so the parameter value stays typed/escaped
            cmd.Parameters.AddWithValue("%" + nameFilter + "%");
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var users = new List<User>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new User(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDateTime(3)
            ));
        }

        return users;
    }

    public async Task<Guid?> FindIdByDisplayNameAsync(string displayName, CancellationToken ct = default)
    {
        const string sql = "SELECT id FROM users WHERE display_name = $1 LIMIT 1";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(displayName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid id ? id : null;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id, display_name, avatar_url, created_at FROM users WHERE id = $1";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new User(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetDateTime(3)
        );
    }
}
