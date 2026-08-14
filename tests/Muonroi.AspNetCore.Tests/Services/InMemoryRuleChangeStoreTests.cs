namespace Muonroi.AspNetCore.Tests.Services;

public class InMemoryRuleChangeStoreTests
{
    private readonly IMDateTimeService _dateTimeService = Substitute.For<IMDateTimeService>();
    private readonly InMemoryRuleChangeStore _store;
    private readonly DateTime _fixedTime = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    public InMemoryRuleChangeStoreTests()
    {
        _dateTimeService.UtcNow().Returns(_fixedTime);
        _store = new InMemoryRuleChangeStore(_dateTimeService);
    }

    [Fact]
    public async Task GetCurrentAsync_NoData_ReturnsEmptyList()
    {
        var result = await _store.GetCurrentAsync("tenant-1", "/api/test");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_StoresNewOrder()
    {
        var request = new RuleOrderChangeRequest
        {
            TenantId = "tenant-1",
            EndpointRoute = "/api/test",
            OrderedRuleCodes = ["rule-a", "rule-b", "rule-c"]
        };

        var record = await _store.ApplyAsync(request, "admin");

        record.TenantId.Should().Be("tenant-1");
        record.EndpointRoute.Should().Be("/api/test");
        record.NewOrder.Should().BeEquivalentTo(["rule-a", "rule-b", "rule-c"]);
        record.PreviousOrder.Should().BeEmpty();
        record.AppliedBy.Should().Be("admin");
        record.AppliedAtUtc.Should().Be(_fixedTime);
    }

    [Fact]
    public async Task ApplyAsync_GetCurrentAsync_ReturnsAppliedOrder()
    {
        var request = new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/route",
            OrderedRuleCodes = ["x", "y"]
        };

        await _store.ApplyAsync(request, "user");
        var current = await _store.GetCurrentAsync("t1", "/route");

        current.Should().BeEquivalentTo(["x", "y"]);
    }

    [Fact]
    public async Task ApplyAsync_DeduplicatesRuleCodes()
    {
        var request = new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/route",
            OrderedRuleCodes = ["rule-a", "RULE-A", "rule-b"]
        };

        var record = await _store.ApplyAsync(request, "user");

        record.NewOrder.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApplyAsync_FiltersOutWhitespaceRuleCodes()
    {
        var request = new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/route",
            OrderedRuleCodes = ["rule-a", "", "  ", "rule-b"]
        };

        var record = await _store.ApplyAsync(request, "user");

        record.NewOrder.Should().BeEquivalentTo(["rule-a", "rule-b"]);
    }

    [Fact]
    public async Task ApplyAsync_BlankAppliedBy_DefaultsToSystem()
    {
        var request = new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/route",
            OrderedRuleCodes = ["a"]
        };

        var record = await _store.ApplyAsync(request, "  ");

        record.AppliedBy.Should().Be("system");
    }

    [Fact]
    public async Task RollbackAsync_NoHistory_ReturnsNull()
    {
        var result = await _store.RollbackAsync("t1", "/route", "admin");
        result.Should().BeNull();
    }

    [Fact]
    public async Task RollbackAsync_RestoresPreviousOrder()
    {
        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/route",
            OrderedRuleCodes = ["a", "b"]
        }, "user");

        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/route",
            OrderedRuleCodes = ["c", "d"]
        }, "user");

        var rollback = await _store.RollbackAsync("t1", "/route", "admin");
        rollback.Should().NotBeNull();
        rollback!.NewOrder.Should().BeEquivalentTo(["a", "b"]);

        var current = await _store.GetCurrentAsync("t1", "/route");
        current.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public async Task GetHistoryAsync_NoHistory_ReturnsEmptyList()
    {
        var result = await _store.GetHistoryAsync("t1", "/route");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsAllRecords()
    {
        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1", EndpointRoute = "/route", OrderedRuleCodes = ["a"]
        }, "user1");

        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1", EndpointRoute = "/route", OrderedRuleCodes = ["b"]
        }, "user2");

        var history = await _store.GetHistoryAsync("t1", "/route");
        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task NormalizeTenantId_NullTenantId_DefaultsToGlobal()
    {
        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = null,
            EndpointRoute = "/route",
            OrderedRuleCodes = ["a"]
        }, "user");

        var current = await _store.GetCurrentAsync("_global", "/route");
        current.Should().Contain("a");
    }

    [Fact]
    public async Task NormalizeRoute_NoLeadingSlash_AddsSlash()
    {
        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "route",
            OrderedRuleCodes = ["a"]
        }, "user");

        var current = await _store.GetCurrentAsync("t1", "/route");
        current.Should().Contain("a");
    }

    [Fact]
    public async Task NormalizeRoute_DoubleSlashes_Normalized()
    {
        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/api//test",
            OrderedRuleCodes = ["a"]
        }, "user");

        var current = await _store.GetCurrentAsync("t1", "/api/test");
        current.Should().Contain("a");
    }

    [Fact]
    public async Task NormalizeRoute_NullRoute_DefaultsToSlash()
    {
        await _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = null!,
            OrderedRuleCodes = ["a"]
        }, "user");

        var current = await _store.GetCurrentAsync("t1", "/");
        current.Should().Contain("a");
    }

    [Fact]
    public async Task ApplyAsync_CancellationToken_ThrowsWhenCancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => _store.ApplyAsync(new RuleOrderChangeRequest
        {
            TenantId = "t1", EndpointRoute = "/", OrderedRuleCodes = ["a"]
        }, "user", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
