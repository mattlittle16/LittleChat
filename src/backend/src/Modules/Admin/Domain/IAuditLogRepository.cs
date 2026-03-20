namespace LittleChat.Modules.Admin.Domain;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> GetAsync(
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default);
}
