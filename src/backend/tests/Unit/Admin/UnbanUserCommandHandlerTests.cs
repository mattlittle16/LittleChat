using Bogus;
using LittleChat.Modules.Admin.Application;
using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Application.Queries;
using LittleChat.Modules.Admin.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Admin;

public class UnbanUserCommandHandlerTests
{
    private readonly IAdminRepository       _repo      = Substitute.For<IAdminRepository>();
    private readonly ITokenBlocklistService _blocklist = Substitute.For<ITokenBlocklistService>();
    private readonly IAuditLogRepository    _auditLog  = Substitute.For<IAuditLogRepository>();
    private readonly IEventBus              _eventBus  = Substitute.For<IEventBus>();
    private static readonly Faker Fake = new();

    private UnbanUserCommandHandler Build() =>
        new(_repo, _blocklist, _auditLog, _eventBus);

    private static AdminUserDto MakeUser(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Fake.Internet.UserName(), null);

    [Fact]
    public async Task Returns_NotFound_when_user_does_not_exist()
    {
        _repo.GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((AdminUserDto?)null);

        var result = await Build().Handle(new UnbanUserCommand(Guid.NewGuid(), Guid.NewGuid(), "admin"), default);

        Assert.IsType<UnbanUserCommandResult.NotFound>(result);
    }

    [Fact]
    public async Task Returns_NotBanned_when_user_has_no_active_ban()
    {
        var targetId = Guid.NewGuid();
        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(MakeUser(targetId));
        _blocklist.GetBanExpiryAsync(targetId, Arg.Any<CancellationToken>())
                  .Returns((DateTimeOffset?)null);

        var result = await Build().Handle(new UnbanUserCommand(targetId, Guid.NewGuid(), "admin"), default);

        Assert.IsType<UnbanUserCommandResult.NotBanned>(result);
    }

    [Fact]
    public async Task Returns_Success_and_unblocks_user_when_banned()
    {
        var targetId   = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        var targetUser = MakeUser(targetId);

        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(targetUser);
        _repo.GetUserByIdAsync(adminId,  Arg.Any<CancellationToken>()).Returns(MakeUser(adminId));
        _blocklist.GetBanExpiryAsync(targetId, Arg.Any<CancellationToken>())
                  .Returns(DateTimeOffset.UtcNow.AddHours(12));

        var result = await Build().Handle(new UnbanUserCommand(targetId, adminId, "admin"), default);

        var success = Assert.IsType<UnbanUserCommandResult.Success>(result);
        Assert.Equal(targetId,               success.UserId);
        Assert.Equal(targetUser.DisplayName, success.DisplayName);
        await _blocklist.Received(1).UnblockUserAsync(targetId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logs_audit_entry_on_successful_unban()
    {
        var targetId = Guid.NewGuid();
        var adminId  = Guid.NewGuid();
        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(MakeUser(targetId));
        _repo.GetUserByIdAsync(adminId,  Arg.Any<CancellationToken>()).Returns(MakeUser(adminId));
        _blocklist.GetBanExpiryAsync(targetId, Arg.Any<CancellationToken>())
                  .Returns(DateTimeOffset.UtcNow.AddHours(1));

        await Build().Handle(new UnbanUserCommand(targetId, adminId, "admin"), default);

        await _auditLog.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e => e.Action == "unban" && e.AdminId == adminId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publishes_unbanned_event()
    {
        var targetId   = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        var targetUser = MakeUser(targetId);
        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(targetUser);
        _repo.GetUserByIdAsync(adminId,  Arg.Any<CancellationToken>()).Returns(MakeUser(adminId));
        _blocklist.GetBanExpiryAsync(targetId, Arg.Any<CancellationToken>())
                  .Returns(DateTimeOffset.UtcNow.AddHours(1));

        await Build().Handle(new UnbanUserCommand(targetId, adminId, "admin"), default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<UserUnbannedIntegrationEvent>(e =>
                e.TargetUserId      == targetId &&
                e.TargetDisplayName == targetUser.DisplayName),
            Arg.Any<CancellationToken>());
    }
}
