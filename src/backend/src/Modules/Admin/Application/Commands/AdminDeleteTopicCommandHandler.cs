using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class AdminDeleteTopicCommandHandler : IRequestHandler<AdminDeleteTopicCommand, AdminDeleteTopicResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;

    public AdminDeleteTopicCommandHandler(IAdminRepository repo, IAuditLogRepository auditLog, IEventBus eventBus)
    {
        _repo     = repo;
        _auditLog = auditLog;
        _eventBus = eventBus;
    }

    public async Task<AdminDeleteTopicResult> Handle(AdminDeleteTopicCommand request, CancellationToken cancellationToken)
    {
        var info = await _repo.GetTopicInfoForDeleteAsync(request.TopicId, cancellationToken);
        if (info is null) return new AdminDeleteTopicResult.TopicNotFound();
        if (info.Value.IsProtected) return new AdminDeleteTopicResult.IsProtected();

        // Signal clients before deleting so they can clean up
        await _eventBus.PublishAsync(new RoomDeletedIntegrationEvent
        {
            RoomId = request.TopicId,
        });

        await _repo.DeleteTopicAsync(request.TopicId, cancellationToken);

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId    = request.AdminId,
            AdminName  = adminName,
            Action     = "delete_topic",
            TargetId   = request.TopicId.ToString(),
            TargetName = info.Value.Name,
        }, cancellationToken);

        return new AdminDeleteTopicResult.Success(request.TopicId, info.Value.Name);
    }
}
