using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class UnbanUserCommandHandler : IRequestHandler<UnbanUserCommand, UnbanUserCommandResult>
{
    private readonly IAdminRepository _repo;
    private readonly ITokenBlocklistService _blocklist;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;

    public UnbanUserCommandHandler(IAdminRepository repo, ITokenBlocklistService blocklist, IAuditLogRepository auditLog, IEventBus eventBus)
    {
        _repo = repo;
        _blocklist = blocklist;
        _auditLog = auditLog;
        _eventBus = eventBus;
    }

    public async Task<UnbanUserCommandResult> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
    {
        var targetUser = await _repo.GetUserByIdAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null)
            return new UnbanUserCommandResult.NotFound();

        var isBanned = await _blocklist.GetBanExpiryAsync(request.TargetUserId, cancellationToken) is not null;
        if (!isBanned)
            return new UnbanUserCommandResult.NotBanned();

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _blocklist.UnblockUserAsync(request.TargetUserId, cancellationToken);

        // Constitution Principle I: persist audit entry before publishing event
        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId = request.AdminId,
            AdminName = adminName,
            Action = "unban",
            TargetId = request.TargetUserId.ToString(),
            TargetName = targetUser.DisplayName
        }, cancellationToken);

        await _eventBus.PublishAsync(new UserUnbannedIntegrationEvent
        {
            TargetUserId      = request.TargetUserId,
            TargetDisplayName = targetUser.DisplayName,
        });

        return new UnbanUserCommandResult.Success(request.TargetUserId, targetUser.DisplayName);
    }
}
