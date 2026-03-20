using MediatR;

namespace LittleChat.Modules.Admin.Application.Queries;

public sealed record GetTopicMembersQuery(Guid TopicId) : IRequest<GetTopicMembersResult?>;

public sealed record GetTopicMembersResult(
    Guid TopicId,
    string TopicName,
    IReadOnlyList<AdminTopicMemberDto> Members);
