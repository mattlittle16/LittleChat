using LittleChat.Modules.Admin.Domain;
using Npgsql;

namespace LittleChat.Modules.Admin.Infrastructure;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AdminDbContext _db;
    private readonly NpgsqlDataSource _dataSource;

    public AuditLogRepository(AdminDbContext db, NpgsqlDataSource dataSource)
    {
        _db = db;
        _dataSource = dataSource;
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        _db.AuditLog.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> GetAsync(
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        await using var countCmd = _dataSource.CreateCommand(@"
            SELECT COUNT(*) FROM admin_audit_log
            WHERE ($1::date IS NULL OR ""OccurredAt"" >= $1::date)
              AND ($2::date IS NULL OR ""OccurredAt"" < ($2::date + INTERVAL '1 day'))");
        countCmd.Parameters.AddWithValue(from.HasValue ? (object)from.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        countCmd.Parameters.AddWithValue(to.HasValue ? (object)to.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var cmd = _dataSource.CreateCommand(@"
            SELECT ""Id"", ""AdminId"", ""AdminName"", ""Action"", ""TargetId"", ""TargetName"", ""OccurredAt""
            FROM admin_audit_log
            WHERE ($1::date IS NULL OR ""OccurredAt"" >= $1::date)
              AND ($2::date IS NULL OR ""OccurredAt"" < ($2::date + INTERVAL '1 day'))
            ORDER BY ""OccurredAt"" DESC
            LIMIT $3 OFFSET $4");
        cmd.Parameters.AddWithValue(from.HasValue ? (object)from.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        cmd.Parameters.AddWithValue(to.HasValue ? (object)to.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        cmd.Parameters.AddWithValue(pageSize);
        cmd.Parameters.AddWithValue(offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<AuditLogEntry>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(new AuditLogEntry
            {
                Id = reader.GetInt64(0),
                AdminId = reader.GetGuid(1),
                AdminName = reader.GetString(2),
                Action = reader.GetString(3),
                TargetId = reader.IsDBNull(4) ? null : reader.GetString(4),
                TargetName = reader.IsDBNull(5) ? null : reader.GetString(5),
                OccurredAt = reader.GetFieldValue<DateTimeOffset>(6)
            });
        }

        return (items, totalCount);
    }
}
