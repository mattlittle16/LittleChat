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

    public async Task<(bool IsNew, Guid UserId)> UpsertAsync(string externalId, string displayName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        // On first login: inserts with a freshly generated UUID.
        // On subsequent logins: conflicts on external_id but does NOT overwrite display_name/avatar
        // (user may have customised them via profile settings).
        const string sql = """
            INSERT INTO users (id, external_id, display_name, avatar_url, created_at)
            VALUES ($1, $2, $3, $4, NOW())
            ON CONFLICT (external_id) DO UPDATE
                SET id = users.id
            RETURNING id, (xmax = 0) AS is_new
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(externalId);
        cmd.Parameters.AddWithValue(displayName);
        cmd.Parameters.AddWithValue(avatarUrl as object ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetBoolean(1), reader.GetGuid(0));
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(string? nameFilter, CancellationToken cancellationToken = default)
    {
        // Both SQL strings are compile-time constants; user input is always bound as a parameter ($1),
        // never interpolated into SQL — no injection risk.
        const string allSql      = "SELECT id, display_name, avatar_url, profile_image_path, crop_x, crop_y, crop_zoom, created_at, onboarding_status, status_emoji, status_text, status_color FROM users ORDER BY display_name";
        const string filteredSql = "SELECT id, display_name, avatar_url, profile_image_path, crop_x, crop_y, crop_zoom, created_at, onboarding_status, status_emoji, status_text, status_color FROM users WHERE display_name ILIKE $1 ORDER BY display_name";

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
            users.Add(MapUser(reader));
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
        const string sql = "SELECT id, display_name, avatar_url, profile_image_path, crop_x, crop_y, crop_zoom, created_at, onboarding_status, status_emoji, status_text, status_color FROM users WHERE id = $1";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapUser(reader);
    }

    public async Task UpdateDisplayNameAsync(Guid id, string displayName, CancellationToken ct = default)
    {
        const string sql = "UPDATE users SET display_name = $1 WHERE id = $2";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(displayName);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAvatarAsync(Guid id, string profileImagePath, float cropX, float cropY, float cropZoom, CancellationToken ct = default)
    {
        const string sql = "UPDATE users SET profile_image_path = $1, crop_x = $2, crop_y = $3, crop_zoom = $4 WHERE id = $5";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(profileImagePath);
        cmd.Parameters.AddWithValue(cropX);
        cmd.Parameters.AddWithValue(cropY);
        cmd.Parameters.AddWithValue(cropZoom);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAvatarAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "UPDATE users SET profile_image_path = NULL, crop_x = NULL, crop_y = NULL, crop_zoom = NULL WHERE id = $1";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<OnboardingStatus> GetOnboardingStatusAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT onboarding_status FROM users WHERE id = $1";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        var result = await cmd.ExecuteScalarAsync(ct);
        return ParseOnboardingStatus(result as string);
    }

    public async Task SetOnboardingStatusAsync(Guid id, OnboardingStatus status, CancellationToken ct = default)
    {
        const string sql = "UPDATE users SET onboarding_status = $1 WHERE id = $2";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(ToDbString(status));
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static OnboardingStatus ParseOnboardingStatus(string? value) => value switch
    {
        "remind_later" => OnboardingStatus.RemindLater,
        "dismissed"    => OnboardingStatus.Dismissed,
        _              => OnboardingStatus.NotStarted,
    };

    private static string ToDbString(OnboardingStatus status) => status switch
    {
        OnboardingStatus.RemindLater => "remind_later",
        OnboardingStatus.Dismissed   => "dismissed",
        _                            => "not_started",
    };

    public async Task UpdateStatusAsync(Guid id, string? emoji, string? text, string? color, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            "UPDATE users SET status_emoji = $1, status_text = $2, status_color = $3 WHERE id = $4");
        cmd.Parameters.AddWithValue(emoji as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue(text as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue(color as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static User MapUser(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetFloat(4),
        reader.IsDBNull(5) ? null : reader.GetFloat(5),
        reader.IsDBNull(6) ? null : reader.GetFloat(6),
        reader.GetDateTime(7),
        ParseOnboardingStatus(reader.GetString(8)),
        StatusEmoji: reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : null,
        StatusText:  reader.FieldCount > 10 && !reader.IsDBNull(10) ? reader.GetString(10) : null,
        StatusColor: reader.FieldCount > 11 && !reader.IsDBNull(11) ? reader.GetString(11) : null
    );
}
