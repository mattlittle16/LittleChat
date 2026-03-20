using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class AdminAddTopicMemberCommandHandler : IRequestHandler<AdminAddTopicMemberCommand, AdminAddTopicMemberResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;

    public AdminAddTopicMemberCommandHandler(IAdminRepository repo, IAuditLogRepository auditLog, IEventBus eventBus)
    {
        _repo = repo;
        _auditLog = auditLog;
        _eventBus = eventBus;
    }

    public async Task<AdminAddTopicMemberResult> Handle(AdminAddTopicMemberCommand request, CancellationToken cancellationToken)
    {
        var topicName = await _repo.GetTopicNameAsync(request.TopicId, cancellationToken);
        if (topicName is null) return new AdminAddTopicMemberResult.TopicNotFound();

        var user = await _repo.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null) return new AdminAddTopicMemberResult.UserNotFound();

        var alreadyMember = await _repo.IsTopicMemberAsync(request.TopicId, request.UserId, cancellationToken);
        if (alreadyMember) return new AdminAddTopicMemberResult.AlreadyMember();

        await _repo.AddTopicMemberAsync(request.TopicId, request.UserId, cancellationToken);

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId    = request.AdminId,
            AdminName  = adminName,
            Action     = "add_topic_member",
            TargetId   = request.UserId.ToString(),
            TargetName = $"{user.DisplayName} → {topicName}",
        }, cancellationToken);

        await _eventBus.PublishAsync(new MemberAddedIntegrationEvent
        {
            RoomId      = request.TopicId,
            AddedUserId = request.UserId,
            RoomName    = topicName,
        });

        await _eventBus.PublishAsync(new AdminAddedTopicMemberIntegrationEvent
        {
            RoomId      = request.TopicId,
            RoomName    = topicName,
            UserId      = request.UserId,
            DisplayName = user.DisplayName,
        });

        return new AdminAddTopicMemberResult.Success(request.UserId, user.DisplayName);
    }
}
