using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class AdminRemoveAvatarCommandHandler : IRequestHandler<AdminRemoveAvatarCommand, AdminRemoveAvatarResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;
    private readonly IFileStorageService _fileStorage;

    public AdminRemoveAvatarCommandHandler(
        IAdminRepository repo,
        IAuditLogRepository auditLog,
        IEventBus eventBus,
        IFileStorageService fileStorage)
    {
        _repo = repo;
        _auditLog = auditLog;
        _eventBus = eventBus;
        _fileStorage = fileStorage;
    }

    public async Task<AdminRemoveAvatarResult> Handle(AdminRemoveAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _repo.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null) return new AdminRemoveAvatarResult.UserNotFound();

        var existingPath = await _repo.GetUserProfileImagePathAsync(request.UserId, cancellationToken);
        if (existingPath != null)
            await _fileStorage.DeleteAsync(existingPath, cancellationToken);

        await _repo.ClearUserAvatarAsync(request.UserId, cancellationToken);

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId    = request.AdminId,
            AdminName  = adminName,
            Action     = "remove_avatar",
            TargetId   = request.UserId.ToString(),
            TargetName = user.DisplayName,
        }, cancellationToken);

        await _eventBus.PublishAsync(new UserProfileUpdatedIntegrationEvent
        {
            UserId          = request.UserId,
            DisplayName     = user.DisplayName,
            ProfileImageUrl = null,
        }, cancellationToken);

        return new AdminRemoveAvatarResult.Success(request.UserId, user.DisplayName);
    }
}
