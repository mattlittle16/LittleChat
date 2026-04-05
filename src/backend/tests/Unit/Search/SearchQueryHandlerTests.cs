using Bogus;
using Search.Application;
using Search.Application.Queries;
using Shared.Contracts.DTOs;

namespace Tests.Unit.Search;

public class SearchQueryHandlerTests
{
    private readonly IMessageSearchRepository _repo = Substitute.For<IMessageSearchRepository>();
    private static readonly Faker Fake = new();

    private SearchQueryHandler Build() => new(_repo);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public async Task Returns_empty_list_for_empty_or_whitespace_query(string? q)
    {
        var query  = new SearchQuery(Guid.NewGuid(), q ?? string.Empty, "global", null);
        var result = await Build().Handle(query, default);

        Assert.Empty(result);
        await _repo.DidNotReceive().SearchAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Room_scope_without_RoomId_throws()
    {
        var query = new SearchQuery(Guid.NewGuid(), "hello", "room", null);

        await Assert.ThrowsAsync<ArgumentException>(() => Build().Handle(query, default));
    }

    [Fact]
    public async Task Global_scope_delegates_to_repo_with_correct_params()
    {
        var userId = Guid.NewGuid();
        var q      = Fake.Lorem.Word();
        var query  = new SearchQuery(userId, q, "global", null);
        _repo.SearchAsync(userId, q, "global", null, Arg.Any<CancellationToken>())
             .Returns(Array.Empty<SearchResultDto>());

        await Build().Handle(query, default);

        await _repo.Received(1).SearchAsync(userId, q, "global", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Room_scope_with_RoomId_delegates_to_repo()
    {
        var userId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var q      = Fake.Lorem.Word();
        var query  = new SearchQuery(userId, q, "room", roomId);
        _repo.SearchAsync(userId, q, "room", roomId, Arg.Any<CancellationToken>())
             .Returns(Array.Empty<SearchResultDto>());

        await Build().Handle(query, default);

        await _repo.Received(1).SearchAsync(userId, q, "room", roomId, Arg.Any<CancellationToken>());
    }
}
