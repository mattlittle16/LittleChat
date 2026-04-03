using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class AdminUpdateDisplayNameCommandHandler : IRequestHandler<AdminUpdateDisplayNameCommand, AdminUpdateDisplayNameResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;

    public AdminUpdateDisplayNameCommandHandler(IAdminRepository repo, IAuditLogRepository auditLog, IEventBus eventBus)
    {
        _repo = repo;
        _auditLog = auditLog;
        _eventBus = eventBus;
    }

    public async Task<AdminUpdateDisplayNameResult> Handle(AdminUpdateDisplayNameCommand request, CancellationToken cancellationToken)
    {
        var trimmed = request.DisplayName.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 50 || trimmed.Contains('@'))
            return new AdminUpdateDisplayNameResult.InvalidName();

        var user = await _repo.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null) return new AdminUpdateDisplayNameResult.UserNotFound();

        await _repo.UpdateUserDisplayNameAsync(request.UserId, trimmed, cancellationToken);

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId    = request.AdminId,
            AdminName  = adminName,
            Action     = "update_display_name",
            TargetId   = request.UserId.ToString(),
            TargetName = $"{user.DisplayName} → {trimmed}",
        }, cancellationToken);

        var profileImagePath = await _repo.GetUserProfileImagePathAsync(request.UserId, cancellationToken);
        var profileImageUrl = profileImagePath != null
            ? $"/api/users/{request.UserId}/avatar?v={profileImagePath.Split('/')[0]}"
            : null;

        await _eventBus.PublishAsync(new UserProfileUpdatedIntegrationEvent
        {
            UserId          = request.UserId,
            DisplayName     = trimmed,
            ProfileImageUrl = profileImageUrl,
        }, cancellationToken);

        return new AdminUpdateDisplayNameResult.Success(request.UserId, trimmed);
    }
}
