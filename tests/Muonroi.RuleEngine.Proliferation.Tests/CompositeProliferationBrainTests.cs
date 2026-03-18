using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class CompositeProliferationBrainTests
{
    private static ProliferationContext CreateContext(int budget = 10) => new()
    {
        Scope = ProliferationScope.Rule,
        CurrentDepth = 0,
        RemainingBudget = budget,
        RuleSetKind = RuleSetKind.Unknown
    };

    [Fact]
    public async Task AnalyzeAsync_SingleBrain_PassesThrough()
    {
        var brain = new FakeBrain("model-A", 3);
        var composite = new CompositeProliferationBrain([brain], CompositeMode.Parallel);

        ProliferationPlan plan = await composite.AnalyzeAsync("TEST", "{}", null, null, CreateContext());

        plan.Scenarios.Should().HaveCount(3);
        plan.AiModelUsed.Should().Be("model-A");
    }

    [Fact]
    public async Task AnalyzeAsync_ParallelMode_MergesResults()
    {
        var brainA = new FakeBrain("model-A", 2);
        var brainB = new FakeBrain("model-B", 3);
        var composite = new CompositeProliferationBrain([brainA, brainB], CompositeMode.Parallel);

        ProliferationPlan plan = await composite.AnalyzeAsync("TEST", "{}", null, null, CreateContext());

        plan.Scenarios.Should().HaveCount(5);
        plan.AiModelUsed.Should().Contain("composite");
    }

    [Fact]
    public async Task AnalyzeAsync_SequentialMode_AccumulatesResults()
    {
        var brainA = new FakeBrain("model-A", 2);
        var brainB = new FakeBrain("model-B", 3);
        var composite = new CompositeProliferationBrain([brainA, brainB], CompositeMode.Sequential);

        ProliferationPlan plan = await composite.AnalyzeAsync("TEST", "{}", null, null, CreateContext());

        plan.Scenarios.Should().HaveCount(5);
    }

    [Fact]
    public async Task AnalyzeAsync_NoBrains_ReturnsEmpty()
    {
        var composite = new CompositeProliferationBrain([], CompositeMode.Parallel);

        ProliferationPlan plan = await composite.AnalyzeAsync("TEST", "{}", null, null, CreateContext());

        plan.Scenarios.Should().BeEmpty();
        plan.AiModelUsed.Should().Be("none");
    }

    [Fact]
    public async Task AnalyzeAsync_WithDeduplicator_RemovesDuplicates()
    {
        // Both brains return same scenario names — dedup should catch
        var brainA = new FakeBrain("model-A", 2, "same-scenario");
        var brainB = new FakeBrain("model-B", 2, "same-scenario");
        var dedup = new InputHashDeduplicator();
        var composite = new CompositeProliferationBrain([brainA, brainB], CompositeMode.Parallel, dedup);

        ProliferationPlan plan = await composite.AnalyzeAsync("TEST", "{}", null, null, CreateContext());

        // Name-based dedup should reduce count
        plan.Scenarios.Count.Should().BeLessThan(4);
    }

    private sealed class FakeBrain(string modelName, int scenarioCount, string? fixedName = null) : IRuleProliferationBrain
    {
        public Task<ProliferationPlan> AnalyzeAsync(
            string seedRuleCode, string ruleSetJson,
            JsonElement? executionResult, JsonElement? factBagSnapshot,
            ProliferationContext context, CancellationToken ct = default)
        {
            using JsonDocument doc = JsonDocument.Parse("""{"test": true}""");
            List<NeuronScenario> scenarios = Enumerable.Range(0, scenarioCount)
                .Select(i => new NeuronScenario
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SeedRuleCode = seedRuleCode,
                    ScenarioName = fixedName ?? $"scenario-{modelName}-{i}",
                    ProliferationReason = "fake",
                    InputFacts = doc.RootElement.Clone(),
                    Status = ScenarioStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                })
                .ToList();

            return Task.FromResult(new ProliferationPlan
            {
                SeedRuleCode = seedRuleCode,
                Scope = context.Scope,
                AiModelUsed = modelName,
                Scenarios = scenarios,
                GenerationDuration = TimeSpan.FromMilliseconds(10)
            });
        }
    }
}
