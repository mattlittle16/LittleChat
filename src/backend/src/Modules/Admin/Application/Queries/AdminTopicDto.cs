namespace LittleChat.Modules.Admin.Application.Queries;

public sealed record AdminTopicDto(Guid Id, string Name, int MemberCount);
