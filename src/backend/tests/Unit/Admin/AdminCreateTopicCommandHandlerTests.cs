using Bogus;
using LittleChat.Modules.Admin.Application;
using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Application.Queries;
using LittleChat.Modules.Admin.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Tests.Unit.Admin;

public class AdminCreateTopicCommandHandlerTests
{
    private readonly IAdminRepository    _repo     = Substitute.For<IAdminRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly IEventBus           _eventBus = Substitute.For<IEventBus>();
    private static readonly Faker Fake = new();

    private AdminCreateTopicCommandHandler Build() =>
        new(_repo, _auditLog, _eventBus);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Returns_InvalidName_for_empty_or_whitespace_name(string name)
    {
        var cmd    = new AdminCreateTopicCommand(name, Guid.NewGuid(), "admin");
        var result = await Build().Handle(cmd, default);

        Assert.IsType<AdminCreateTopicResult.InvalidName>(result);
        await _repo.DidNotReceive().CreateTopicAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_Success_with_topic_id_and_name()
    {
        var adminId  = Guid.NewGuid();
        var topicId  = Guid.NewGuid();
        var topicName = Fake.Commerce.Department();
        _repo.CreateTopicAsync(topicName, adminId, Arg.Any<CancellationToken>()).Returns(topicId);
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(new AdminUserDto(adminId, "Admin User", null));

        var result = await Build().Handle(new AdminCreateTopicCommand(topicName, adminId, "Admin User"), default);

        var success = Assert.IsType<AdminCreateTopicResult.Success>(result);
        Assert.Equal(topicId,   success.TopicId);
        Assert.Equal(topicName, success.Name);
    }

    [Fact]
    public async Task Publishes_room_created_event()
    {
        var adminId   = Guid.NewGuid();
        var topicId   = Guid.NewGuid();
        var topicName = Fake.Commerce.Department();
        _repo.CreateTopicAsync(topicName, adminId, Arg.Any<CancellationToken>()).Returns(topicId);
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(new AdminUserDto(adminId, "Admin", null));

        await Build().Handle(new AdminCreateTopicCommand(topicName, adminId, "Admin"), default);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<RoomCreatedIntegrationEvent>(e =>
                e.RoomId    == topicId   &&
                e.Name      == topicName &&
                e.IsDm      == false     &&
                e.CreatedBy == adminId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logs_audit_entry_with_create_topic_action()
    {
        var adminId   = Guid.NewGuid();
        var topicId   = Guid.NewGuid();
        var topicName = Fake.Commerce.Department();
        _repo.CreateTopicAsync(topicName, adminId, Arg.Any<CancellationToken>()).Returns(topicId);
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(new AdminUserDto(adminId, "Admin", null));

        await Build().Handle(new AdminCreateTopicCommand(topicName, adminId, "Admin"), default);

        await _auditLog.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.Action     == "create_topic" &&
                e.AdminId    == adminId        &&
                e.TargetName == topicName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Trims_whitespace_from_name_before_creating()
    {
        var adminId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _repo.CreateTopicAsync("general", adminId, Arg.Any<CancellationToken>()).Returns(topicId);
        _repo.GetUserByIdAsync(adminId, Arg.Any<CancellationToken>())
             .Returns(new AdminUserDto(adminId, "Admin", null));

        var result = await Build().Handle(
            new AdminCreateTopicCommand("  general  ", adminId, "Admin"), default);

        // Should succeed (not return InvalidName) since trimmed value is valid
        Assert.IsType<AdminCreateTopicResult.Success>(result);
        await _repo.Received(1).CreateTopicAsync("general", adminId, Arg.Any<CancellationToken>());
    }
}
