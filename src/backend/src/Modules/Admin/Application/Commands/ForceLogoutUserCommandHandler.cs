using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class ForceLogoutUserCommandHandler : IRequestHandler<ForceLogoutUserCommand, ForceLogoutUserCommandResult>
{
    private readonly IAdminRepository _repo;
    private readonly ITokenBlocklistService _blocklist;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;

    public ForceLogoutUserCommandHandler(
        IAdminRepository repo,
        ITokenBlocklistService blocklist,
        IAuditLogRepository auditLog,
        IEventBus eventBus)
    {
        _repo = repo;
        _blocklist = blocklist;
        _auditLog = auditLog;
        _eventBus = eventBus;
    }

    public async Task<ForceLogoutUserCommandResult> Handle(ForceLogoutUserCommand request, CancellationToken cancellationToken)
    {
        var targetUser = await _repo.GetUserByIdAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null)
            return new ForceLogoutUserCommandResult.NotFound();

        if (request.TargetUserId == request.AdminId)
            return new ForceLogoutUserCommandResult.SelfLogout();

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        // Block user in Redis for the requested duration
        await _blocklist.BlockUserAsync(request.TargetUserId, TimeSpan.FromHours(request.BanDurationHours), cancellationToken);

        // Constitution Principle I: persist audit entry BEFORE publishing integration event
        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId = request.AdminId,
            AdminName = adminName,
            Action = "force_logout",
            TargetId = request.TargetUserId.ToString(),
            TargetName = targetUser.DisplayName
        }, cancellationToken);

        // Publish events AFTER audit is persisted (Constitution Principle I)
        await _eventBus.PublishAsync(new UserForceLoggedOutIntegrationEvent
        {
            TargetUserId = request.TargetUserId
        });

        await _eventBus.PublishAsync(new UserBannedIntegrationEvent
        {
            TargetUserId      = request.TargetUserId,
            TargetDisplayName = targetUser.DisplayName,
            BannedUntil       = DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(request.BanDurationHours))
        });

        return new ForceLogoutUserCommandResult.Success(request.TargetUserId, targetUser.DisplayName);
    }
}
