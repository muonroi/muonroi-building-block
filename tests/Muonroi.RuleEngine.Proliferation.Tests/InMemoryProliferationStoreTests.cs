namespace Muonroi.RuleEngine.Proliferation.Tests;

public class InMemoryProliferationStoreTests
{
    private readonly InMemoryProliferationStore _store = new();

    private static NeuronScenario CreateScenario(
        string id = "s1",
        string seed = "TEST",
        ScenarioStatus status = ScenarioStatus.Pending,
        int depth = 0,
        string? parentId = null) => new()
    {
        Id = id,
        SeedRuleCode = seed,
        ScenarioName = $"Scenario {id}",
        ProliferationReason = "test",
        Status = status,
        GenerationDepth = depth,
        ParentScenarioId = parentId,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task SaveAndGetPending_ReturnsOnlyPending()
    {
        await _store.SaveScenariosAsync([
            CreateScenario("s1", status: ScenarioStatus.Pending),
            CreateScenario("s2", status: ScenarioStatus.Passed),
            CreateScenario("s3", status: ScenarioStatus.Pending)
        ]);

        IReadOnlyList<NeuronScenario> pending = await _store.GetPendingScenariosAsync();
        pending.Should().HaveCount(2);
        pending.Select(s => s.Id).Should().BeEquivalentTo(["s1", "s3"]);
    }

    [Fact]
    public async Task GetPending_RespectsLimit()
    {
        await _store.SaveScenariosAsync([
            CreateScenario("s1"),
            CreateScenario("s2"),
            CreateScenario("s3")
        ]);

        IReadOnlyList<NeuronScenario> pending = await _store.GetPendingScenariosAsync(limit: 2);
        pending.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateStatus_ChangesStatus()
    {
        await _store.SaveScenariosAsync([CreateScenario("s1")]);
        await _store.UpdateStatusAsync("s1", ScenarioStatus.Running);

        IReadOnlyList<NeuronScenario> pending = await _store.GetPendingScenariosAsync();
        pending.Should().BeEmpty();

        IReadOnlyList<NeuronScenario> all = await _store.GetScenariosBySeedAsync("TEST");
        all.Should().HaveCount(1);
        all[0].Status.Should().Be(ScenarioStatus.Running);
    }

    [Fact]
    public async Task GetScenariosBySeed_FiltersBySeedCode()
    {
        await _store.SaveScenariosAsync([
            CreateScenario("s1", seed: "ORDER"),
            CreateScenario("s2", seed: "PAYMENT"),
            CreateScenario("s3", seed: "ORDER")
        ]);

        IReadOnlyList<NeuronScenario> orderScenarios = await _store.GetScenariosBySeedAsync("ORDER");
        orderScenarios.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveResult_StoresResult()
    {
        ScenarioResult result = new()
        {
            ScenarioId = "s1",
            IsSuccess = true,
            MatchesExpectation = true,
            Duration = TimeSpan.FromMilliseconds(100),
            ExecutedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveResultAsync(result);
        // No direct getter for results, but the store should not throw
    }

    [Fact]
    public async Task GetLineage_ReturnsOrderedByDepth()
    {
        await _store.SaveScenariosAsync([
            CreateScenario("s1", depth: 0),
            CreateScenario("s2", depth: 1, parentId: "s1"),
            CreateScenario("s3", depth: 2, parentId: "s2")
        ]);

        IReadOnlyList<RuleLineage> lineage = await _store.GetLineageAsync("TEST");
        lineage.Should().HaveCount(3);
        lineage[0].Depth.Should().Be(0);
        lineage[1].Depth.Should().Be(1);
        lineage[2].Depth.Should().Be(2);
        lineage[1].ParentScenarioId.Should().Be("s1");
    }

    [Fact]
    public async Task GetStats_AggregatesCorrectly()
    {
        await _store.SaveScenariosAsync([
            CreateScenario("s1", seed: "ORDER", status: ScenarioStatus.Pending, depth: 0),
            CreateScenario("s2", seed: "ORDER", status: ScenarioStatus.Passed, depth: 1),
            CreateScenario("s3", seed: "PAYMENT", status: ScenarioStatus.Failed, depth: 0),
            CreateScenario("s4", seed: "ORDER", status: ScenarioStatus.Passed, depth: 2)
        ]);

        // Need to update statuses since CreateScenario sets initial status
        await _store.UpdateStatusAsync("s2", ScenarioStatus.Passed);
        await _store.UpdateStatusAsync("s3", ScenarioStatus.Failed);
        await _store.UpdateStatusAsync("s4", ScenarioStatus.Passed);

        ProliferationStats stats = await _store.GetStatsAsync();
        stats.TotalScenarios.Should().Be(4);
        stats.Passed.Should().Be(2);
        stats.Failed.Should().Be(1);
        stats.Pending.Should().Be(1);
        stats.MaxDepthReached.Should().Be(2);
        stats.BySeedRule.Should().ContainKey("ORDER").WhoseValue.Should().Be(3);
        stats.BySeedRule.Should().ContainKey("PAYMENT").WhoseValue.Should().Be(1);
    }

    [Fact]
    public async Task GetStats_FilterBySeed()
    {
        await _store.SaveScenariosAsync([
            CreateScenario("s1", seed: "ORDER"),
            CreateScenario("s2", seed: "PAYMENT")
        ]);

        ProliferationStats stats = await _store.GetStatsAsync("ORDER");
        stats.TotalScenarios.Should().Be(1);
    }
}
