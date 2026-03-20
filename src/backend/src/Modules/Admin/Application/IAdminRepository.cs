using LittleChat.Modules.Admin.Application.Queries;

namespace LittleChat.Modules.Admin.Application;

public interface IAdminRepository
{
    Task<(IReadOnlyList<AdminUserDto> Items, int TotalCount)> GetUsersAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    Task<AdminUserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminTopicDto> Items, int TotalCount)> GetTopicsAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    Task<(string? TopicName, IReadOnlyList<AdminTopicMemberDto>? Members)> GetTopicMembersAsync(
        Guid topicId, CancellationToken ct = default);

    Task<string?> GetTopicNameAsync(Guid topicId, CancellationToken ct = default);
    Task<bool> IsTopicMemberAsync(Guid topicId, Guid userId, CancellationToken ct = default);
    Task AddTopicMemberAsync(Guid topicId, Guid userId, CancellationToken ct = default);
    Task RemoveTopicMemberAsync(Guid topicId, Guid userId, CancellationToken ct = default);
}
