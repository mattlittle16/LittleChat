using Bogus;
using LittleChat.Modules.Admin.Application;
using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Application.Queries;
using LittleChat.Modules.Admin.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Admin;

public class ForceLogoutUserCommandHandlerTests
{
    private readonly IAdminRepository       _repo      = Substitute.For<IAdminRepository>();
    private readonly ITokenBlocklistService _blocklist = Substitute.For<ITokenBlocklistService>();
    private readonly IAuditLogRepository    _auditLog  = Substitute.For<IAuditLogRepository>();
    private readonly IEventBus              _eventBus  = Substitute.For<IEventBus>();
    private static readonly Faker Fake = new();

    private ForceLogoutUserCommandHandler Build() =>
        new(_repo, _blocklist, _auditLog, _eventBus);

    private static AdminUserDto MakeUser(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Fake.Internet.UserName(), null);

    [Fact]
    public async Task Returns_NotFound_when_target_user_does_not_exist()
    {
        _repo.GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((AdminUserDto?)null);

        var cmd = new ForceLogoutUserCommand(Guid.NewGuid(), Guid.NewGuid(), "admin", 24);
        var result = await Build().Handle(cmd, default);

        Assert.IsType<ForceLogoutUserCommandResult.NotFound>(result);
    }

    [Fact]
    public async Task Returns_SelfLogout_when_admin_targets_themselves()
    {
        var adminId = Guid.NewGuid();
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(MakeUser(adminId));

        var cmd = new ForceLogoutUserCommand(
            TargetUserId: adminId, AdminId: adminId, AdminName: "admin", BanDurationHours: 24);
        var result = await Build().Handle(cmd, default);

        Assert.IsType<ForceLogoutUserCommandResult.SelfLogout>(result);
    }

    [Fact]
    public async Task Returns_Success_with_correct_user_on_valid_ban()
    {
        var targetId   = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        var targetUser = MakeUser(targetId);
        var adminUser  = MakeUser(adminId);

        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(targetUser);
        _repo.GetUserByIdAsync(adminId,  Arg.Any<CancellationToken>()).Returns(adminUser);

        var cmd    = new ForceLogoutUserCommand(targetId, adminId, adminUser.DisplayName, 48);
        var result = await Build().Handle(cmd, default);

        var success = Assert.IsType<ForceLogoutUserCommandResult.Success>(result);
        Assert.Equal(targetId,              success.UserId);
        Assert.Equal(targetUser.DisplayName, success.DisplayName);
    }

    [Fact]
    public async Task Blocks_user_for_specified_duration()
    {
        var targetId   = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        var targetUser = MakeUser(targetId);
        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(targetUser);
        _repo.GetUserByIdAsync(adminId,  Arg.Any<CancellationToken>()).Returns(MakeUser(adminId));

        await Build().Handle(new ForceLogoutUserCommand(targetId, adminId, "admin", 72), default);

        await _blocklist.Received(1).BlockUserAsync(
            targetId, TimeSpan.FromHours(72), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Writes_audit_log_before_publishing_events_constitutionPrincipleI()
    {
        var targetId   = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(MakeUser(targetId));
        _repo.GetUserByIdAsync(adminId,  Arg.Any<CancellationToken>()).Returns(MakeUser(adminId));

        var callOrder = new List<string>();
        _auditLog.AddAsync(Arg.Any<AuditLogEntry>(), Arg.Any<CancellationToken>())
                 .Returns(ci => { callOrder.Add("audit"); return Task.CompletedTask; });
        _eventBus.PublishAsync(Arg.Any<UserForceLoggedOutIntegrationEvent>(), Arg.Any<CancellationToken>())
                 .Returns(ci => { callOrder.Add("event"); return Task.CompletedTask; });

        await Build().Handle(new ForceLogoutUserCommand(targetId, adminId, "admin", 24), default);

        Assert.Equal("audit", callOrder[0]);
    }

    [Fact]
    public async Task Publishes_force_logout_and_banned_events()
    {
        var targetId   = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        _repo.GetUserByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(MakeUser(targetId));
        _repo.GetUserByIdAsync(adminId,  Arg.Any<CancellationToken>()).Returns(MakeUser(adminId));

        await Build().Handle(new ForceLogoutUserCommand(targetId, adminId, "admin", 24), default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<UserForceLoggedOutIntegrationEvent>(e => e.TargetUserId == targetId),
            Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<UserBannedIntegrationEvent>(e => e.TargetUserId == targetId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_block_or_log_for_self_logout()
    {
        var adminId = Guid.NewGuid();
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>()).Returns(MakeUser(adminId));

        await Build().Handle(new ForceLogoutUserCommand(adminId, adminId, "admin", 24), default);

        await _blocklist.DidNotReceive().BlockUserAsync(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().AddAsync(Arg.Any<AuditLogEntry>(), Arg.Any<CancellationToken>());
    }
}
