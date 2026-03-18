using System.Text.Json;
using FluentAssertions;
using Moq;
using Muonroi.RuleEngine.Proliferation.Models;
using Muonroi.RuleEngine.Proliferation.Worker;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ProliferationWorkerTests
{
    private readonly ProliferationOptions _options = new()
    {
        MaxGenerationDepth = 3,
        MaxTotalScenarios = 50,
        MaxScenariosPerRule = 10,
        WorkerIntervalSeconds = 1
    };

    private readonly Mock<IProliferationStore> _store = new();
    private readonly Mock<IScenarioExecutor> _executor = new();
    private readonly Mock<IRuleProliferationBrain> _brain = new();

    private ProliferationWorker CreateWorker() =>
        new(_store.Object, _executor.Object, _brain.Object, _options);

    [Fact]
    public async Task RunCycle_MaxTotalScenarios_SkipsCycle()
    {
        _store.Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationStats { TotalScenarios = 50 });

        ProliferationWorker worker = CreateWorker();
        await worker.RunCycleAsync(CancellationToken.None);

        _store.Verify(s => s.GetPendingScenariosAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCycle_NoPending_DoesNothing()
    {
        _store.Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationStats());
        _store.Setup(s => s.GetPendingScenariosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NeuronScenario>());

        ProliferationWorker worker = CreateWorker();
        await worker.RunCycleAsync(CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(It.IsAny<NeuronScenario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCycle_ExecutesPendingScenarios()
    {
        NeuronScenario scenario = new()
        {
            Id = "s1",
            SeedRuleCode = "TEST",
            ScenarioName = "Test scenario",
            ProliferationReason = "unit test",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _store.Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationStats { TotalScenarios = 1 });
        _store.Setup(s => s.GetPendingScenariosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { scenario });

        _executor.Setup(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScenarioResult
            {
                ScenarioId = "s1",
                IsSuccess = true,
                MatchesExpectation = true,
                Duration = TimeSpan.FromMilliseconds(100),
                ExecutedAt = DateTimeOffset.UtcNow
            });

        _brain.Setup(b => b.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
                It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationPlan
            {
                SeedRuleCode = "TEST",
                AiModelUsed = "test",
                Scenarios = []
            });

        ProliferationWorker worker = CreateWorker();
        await worker.RunCycleAsync(CancellationToken.None);

        _store.Verify(s => s.UpdateStatusAsync("s1", ScenarioStatus.Running, It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.SaveResultAsync(It.IsAny<ScenarioResult>(), It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.UpdateStatusAsync("s1", ScenarioStatus.Passed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunCycle_MaxDepthReached_DoesNotGenerateChildren()
    {
        NeuronScenario scenario = new()
        {
            Id = "s1",
            SeedRuleCode = "TEST",
            ScenarioName = "Deep scenario",
            ProliferationReason = "unit test",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 3, // MaxGenerationDepth = 3
            CreatedAt = DateTimeOffset.UtcNow
        };

        _store.Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationStats { TotalScenarios = 5 });
        _store.Setup(s => s.GetPendingScenariosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { scenario });

        _executor.Setup(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScenarioResult
            {
                ScenarioId = "s1",
                IsSuccess = true,
                MatchesExpectation = true,
                Duration = TimeSpan.FromMilliseconds(50),
                ExecutedAt = DateTimeOffset.UtcNow
            });

        ProliferationWorker worker = CreateWorker();
        await worker.RunCycleAsync(CancellationToken.None);

        // Brain should NOT be called because depth limit is reached
        _brain.Verify(b => b.AnalyzeAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
            It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCycle_FailedScenario_SetsErrorStatus()
    {
        NeuronScenario scenario = new()
        {
            Id = "s1",
            SeedRuleCode = "TEST",
            ScenarioName = "Failing scenario",
            ProliferationReason = "unit test",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _store.Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationStats());
        _store.Setup(s => s.GetPendingScenariosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { scenario });

        _executor.Setup(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScenarioResult
            {
                ScenarioId = "s1",
                IsSuccess = false,
                MatchesExpectation = false,
                ActualBehavior = "failed: validation error",
                Errors = ["validation error"],
                Duration = TimeSpan.FromMilliseconds(50),
                ExecutedAt = DateTimeOffset.UtcNow
            });

        ProliferationWorker worker = CreateWorker();
        await worker.RunCycleAsync(CancellationToken.None);

        _store.Verify(s => s.UpdateStatusAsync("s1", ScenarioStatus.Failed, It.IsAny<CancellationToken>()), Times.Once);
        // Brain should NOT be called for failed scenarios
        _brain.Verify(b => b.AnalyzeAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
            It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
