using Bogus;
using LittleChat.Modules.Admin.Application;
using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Application.Queries;
using LittleChat.Modules.Admin.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Admin;

public class AdminDeleteTopicCommandHandlerTests
{
    private readonly IAdminRepository    _repo     = Substitute.For<IAdminRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly IEventBus           _eventBus = Substitute.For<IEventBus>();
    private static readonly Faker Fake = new();

    private AdminDeleteTopicCommandHandler Build() =>
        new(_repo, _auditLog, _eventBus);

    [Fact]
    public async Task Returns_TopicNotFound_when_topic_does_not_exist()
    {
        _repo.GetTopicInfoForDeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((ValueTuple<string, bool, bool>?)null);

        var result = await Build().Handle(
            new AdminDeleteTopicCommand(Guid.NewGuid(), Guid.NewGuid(), "admin"), default);

        Assert.IsType<AdminDeleteTopicResult.TopicNotFound>(result);
    }

    [Fact]
    public async Task Returns_IsProtected_for_protected_topics()
    {
        var topicId = Guid.NewGuid();
        _repo.GetTopicInfoForDeleteAsync(topicId, Arg.Any<CancellationToken>())
             .Returns(("General", IsProtected: true, IsDm: false));

        var result = await Build().Handle(
            new AdminDeleteTopicCommand(topicId, Guid.NewGuid(), "admin"), default);

        Assert.IsType<AdminDeleteTopicResult.IsProtected>(result);
        await _repo.DidNotReceive().DeleteTopicAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_Success_with_topic_name()
    {
        var topicId   = Guid.NewGuid();
        var adminId   = Guid.NewGuid();
        var topicName = Fake.Commerce.Department();
        _repo.GetTopicInfoForDeleteAsync(topicId, Arg.Any<CancellationToken>())
             .Returns((topicName, IsProtected: false, IsDm: false));
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(new AdminUserDto(adminId, "Admin", null));

        var result = await Build().Handle(
            new AdminDeleteTopicCommand(topicId, adminId, "Admin"), default);

        var success = Assert.IsType<AdminDeleteTopicResult.Success>(result);
        Assert.Equal(topicId,   success.TopicId);
        Assert.Equal(topicName, success.Name);
    }

    [Fact]
    public async Task Publishes_event_before_deleting_constitutionPrincipleI()
    {
        var topicId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _repo.GetTopicInfoForDeleteAsync(topicId, Arg.Any<CancellationToken>())
             .Returns(("test", IsProtected: false, IsDm: false));
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(new AdminUserDto(adminId, "Admin", null));

        var callOrder = new List<string>();
        _eventBus.PublishAsync(Arg.Any<RoomDeletedIntegrationEvent>(), Arg.Any<CancellationToken>())
                 .Returns(ci => { callOrder.Add("event"); return Task.CompletedTask; });
        _repo.DeleteTopicAsync(topicId, Arg.Any<CancellationToken>())
             .Returns(ci => { callOrder.Add("delete"); return Task.CompletedTask; });

        await Build().Handle(new AdminDeleteTopicCommand(topicId, adminId, "Admin"), default);

        Assert.Equal(["event", "delete"], callOrder);
    }

    [Fact]
    public async Task Logs_audit_entry_with_delete_topic_action()
    {
        var topicId   = Guid.NewGuid();
        var adminId   = Guid.NewGuid();
        var topicName = Fake.Commerce.Department();
        _repo.GetTopicInfoForDeleteAsync(topicId, Arg.Any<CancellationToken>())
             .Returns((topicName, IsProtected: false, IsDm: false));
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(new AdminUserDto(adminId, "Admin", null));

        await Build().Handle(new AdminDeleteTopicCommand(topicId, adminId, "Admin"), default);

        await _auditLog.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.Action     == "delete_topic" &&
                e.AdminId    == adminId        &&
                e.TargetName == topicName),
            Arg.Any<CancellationToken>());
    }
}
