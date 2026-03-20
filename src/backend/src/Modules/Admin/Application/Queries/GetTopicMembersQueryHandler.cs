using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed class GetTopicMembersQueryHandler : IRequestHandler<GetTopicMembersQuery, GetTopicMembersResult?>
{
    private readonly IAdminRepository _repo;

    public GetTopicMembersQueryHandler(IAdminRepository repo)
    {
        _repo = repo;
    }

    public async Task<GetTopicMembersResult?> Handle(GetTopicMembersQuery request, CancellationToken cancellationToken)
    {
        var (topicName, members) = await _repo.GetTopicMembersAsync(request.TopicId, cancellationToken);
        if (topicName is null || members is null) return null;

        return new GetTopicMembersResult(request.TopicId, topicName, members);
    }
}
