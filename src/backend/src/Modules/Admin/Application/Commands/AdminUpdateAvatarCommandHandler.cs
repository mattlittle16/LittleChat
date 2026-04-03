using LittleChat.Modules.Admin.Domain;
using MediatR;
using Shared.Contracts;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.Application.Commands;

public sealed class AdminUpdateAvatarCommandHandler : IRequestHandler<AdminUpdateAvatarCommand, AdminUpdateAvatarResult>
{
    private readonly IAdminRepository _repo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IEventBus _eventBus;
    private readonly IFileStorageService _fileStorage;

    public AdminUpdateAvatarCommandHandler(
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

    public async Task<AdminUpdateAvatarResult> Handle(AdminUpdateAvatarCommand request, CancellationToken cancellationToken)
    {
        var magicError = await FileMagicBytes.ValidateAsync(request.FileStream, "jpg", cancellationToken);
        if (magicError is not null) return new AdminUpdateAvatarResult.InvalidFile();

        var user = await _repo.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null) return new AdminUpdateAvatarResult.UserNotFound();

        var existingPath = await _repo.GetUserProfileImagePathAsync(request.UserId, cancellationToken);
        if (existingPath != null)
            await _fileStorage.DeleteAsync(existingPath, cancellationToken);

        var saved = await _fileStorage.SaveAsync(request.FileStream, request.FileName, cancellationToken);
        await _repo.UpdateUserAvatarAsync(request.UserId, saved.RelativePath, cancellationToken);

        var profileImageUrl = $"/api/users/{request.UserId}/avatar?v={saved.RelativePath.Split('/')[0]}";

        var adminUser = await _repo.GetUserByIdAsync(request.AdminId, cancellationToken);
        var adminName = adminUser?.DisplayName ?? request.AdminName;

        await _auditLog.AddAsync(new AuditLogEntry
        {
            AdminId    = request.AdminId,
            AdminName  = adminName,
            Action     = "update_avatar",
            TargetId   = request.UserId.ToString(),
            TargetName = user.DisplayName,
        }, cancellationToken);

        await _eventBus.PublishAsync(new UserProfileUpdatedIntegrationEvent
        {
            UserId          = request.UserId,
            DisplayName     = user.DisplayName,
            ProfileImageUrl = profileImageUrl,
        }, cancellationToken);

        return new AdminUpdateAvatarResult.Success(request.UserId, user.DisplayName, profileImageUrl);
    }
}
