using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class AdminCreateTopicCommandHandler : IRequestHandler<AdminCreateTopicCommand, AdminCreateTopicResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;

    public AdminCreateTopicCommandHandler(IAdminRepository repo, IAuditLogRepository auditLog, IEventBus eventBus)
    {
        _repo     = repo;
        _auditLog = auditLog;
        _eventBus = eventBus;
    }

    public async Task<AdminCreateTopicResult> Handle(AdminCreateTopicCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new AdminCreateTopicResult.InvalidName();

        var topicId = await _repo.CreateTopicAsync(name, request.AdminId, cancellationToken);

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId    = request.AdminId,
            AdminName  = adminName,
            Action     = "create_topic",
            TargetId   = topicId.ToString(),
            TargetName = name,
        }, cancellationToken);

        await _eventBus.PublishAsync(new RoomCreatedIntegrationEvent
        {
            RoomId    = topicId,
            Name      = name,
            IsDm      = false,
            CreatedBy = request.AdminId,
        });

        return new AdminCreateTopicResult.Success(topicId, name);
    }
}
