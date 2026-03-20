using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class AdminRemoveTopicMemberCommandHandler : IRequestHandler<AdminRemoveTopicMemberCommand, AdminRemoveTopicMemberResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;

    public AdminRemoveTopicMemberCommandHandler(IAdminRepository repo, IAuditLogRepository auditLog, IEventBus eventBus)
    {
        _repo = repo;
        _auditLog = auditLog;
        _eventBus = eventBus;
    }

    public async Task<AdminRemoveTopicMemberResult> Handle(AdminRemoveTopicMemberCommand request, CancellationToken cancellationToken)
    {
        var topicName = await _repo.GetTopicNameAsync(request.TopicId, cancellationToken);
        if (topicName is null) return new AdminRemoveTopicMemberResult.TopicNotFound();

        var user = await _repo.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null) return new AdminRemoveTopicMemberResult.UserNotFound();

        var isMember = await _repo.IsTopicMemberAsync(request.TopicId, request.UserId, cancellationToken);
        if (!isMember) return new AdminRemoveTopicMemberResult.NotMember();

        await _repo.RemoveTopicMemberAsync(request.TopicId, request.UserId, cancellationToken);

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId    = request.AdminId,
            AdminName  = adminName,
            Action     = "remove_topic_member",
            TargetId   = request.UserId.ToString(),
            TargetName = $"{user.DisplayName} → {topicName}",
        }, cancellationToken);

        await _eventBus.PublishAsync(new MemberRemovedIntegrationEvent
        {
            RoomId         = request.TopicId,
            RemovedUserId  = request.UserId,
        });

        await _eventBus.PublishAsync(new AdminRemovedTopicMemberIntegrationEvent
        {
            RoomId      = request.TopicId,
            RoomName    = topicName,
            UserId      = request.UserId,
            DisplayName = user.DisplayName,
        });

        return new AdminRemoveTopicMemberResult.Success(request.UserId, user.DisplayName);
    }
}
