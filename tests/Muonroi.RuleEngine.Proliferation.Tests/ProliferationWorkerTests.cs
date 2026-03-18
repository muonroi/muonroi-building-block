using System.Text.Json;
using FluentAssertions;
using Moq;
using Muonroi.RuleEngine.Proliferation.Brain;
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
        MaxScenariosPerFailure = 2,
        EnableFailureFeedback = true,
        WorkerIntervalSeconds = 1
    };

    private readonly Mock<IProliferationStore> _store = new();
    private readonly Mock<IScenarioExecutor> _executor = new();
    private readonly Mock<IRuleProliferationBrain> _brain = new();
    private readonly Mock<IProliferationChangeNotifier> _notifier = new();
    private readonly Mock<IFailureAnalyzer> _failureAnalyzer = new();
    private readonly Mock<IScenarioDeduplicator> _deduplicator = new();

    private ProliferationWorker CreateWorker(
        IProliferationChangeNotifier? notifier = null,
        IFailureAnalyzer? failureAnalyzer = null,
        IScenarioDeduplicator? dedup = null) =>
        new(_store.Object, _executor.Object, _brain.Object, _options,
            notifier: notifier, failureAnalyzer: failureAnalyzer, deduplicator: dedup);

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
        _brain.Verify(b => b.AnalyzeAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
            It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCycle_NotifiesOnScenarioCompletion()
    {
        NeuronScenario scenario = new()
        {
            Id = "s1",
            SeedRuleCode = "TEST",
            ScenarioName = "Notified scenario",
            ProliferationReason = "unit test",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 3, // At max depth, no children generated
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
                Duration = TimeSpan.FromMilliseconds(50),
                ExecutedAt = DateTimeOffset.UtcNow
            });

        ProliferationWorker worker = CreateWorker(_notifier.Object);
        await worker.RunCycleAsync(CancellationToken.None);

        _notifier.Verify(n => n.NotifyScenarioCompletedAsync("s1", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunCycle_WithoutNotifier_DoesNotThrow()
    {
        NeuronScenario scenario = new()
        {
            Id = "s1",
            SeedRuleCode = "TEST",
            ScenarioName = "No notifier",
            ProliferationReason = "unit test",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 3,
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
                Duration = TimeSpan.FromMilliseconds(50),
                ExecutedAt = DateTimeOffset.UtcNow
            });

        ProliferationWorker worker = CreateWorker(notifier: null);
        Func<Task> act = () => worker.RunCycleAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunCycle_FailedScenario_CallsFailureAnalyzer()
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
            .ReturnsAsync(new ProliferationStats { TotalScenarios = 5 });
        _store.Setup(s => s.GetPendingScenariosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { scenario });

        ScenarioResult failureResult = new()
        {
            ScenarioId = "s1",
            IsSuccess = false,
            MatchesExpectation = false,
            ActualBehavior = "Error occurred",
            Errors = ["NullReferenceException"],
            Duration = TimeSpan.FromMilliseconds(50),
            ExecutedAt = DateTimeOffset.UtcNow
        };

        _executor.Setup(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        NeuronScenario feedbackChild = new()
        {
            Id = "feedback-1",
            SeedRuleCode = "TEST",
            ScenarioName = "Feedback probe",
            ProliferationReason = "probing failure",
            ParentScenarioId = "s1",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _failureAnalyzer.Setup(f => f.AnalyzeFailureAsync(
                scenario, failureResult, It.IsAny<string>(),
                It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NeuronScenario> { feedbackChild });

        ProliferationWorker worker = CreateWorker(failureAnalyzer: _failureAnalyzer.Object);
        await worker.RunCycleAsync(CancellationToken.None);

        _failureAnalyzer.Verify(f => f.AnalyzeFailureAsync(
            scenario, failureResult, It.IsAny<string>(),
            It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.SaveScenariosAsync(
            It.Is<IReadOnlyList<NeuronScenario>>(list => list.Any(c => c.Id == "feedback-1")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunCycle_FailedAtMaxDepth_DoesNotCallFailureAnalyzer()
    {
        NeuronScenario scenario = new()
        {
            Id = "s1",
            SeedRuleCode = "TEST",
            ScenarioName = "Deep failing scenario",
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
                IsSuccess = false,
                MatchesExpectation = false,
                Duration = TimeSpan.FromMilliseconds(50),
                ExecutedAt = DateTimeOffset.UtcNow
            });

        ProliferationWorker worker = CreateWorker(failureAnalyzer: _failureAnalyzer.Object);
        await worker.RunCycleAsync(CancellationToken.None);

        _failureAnalyzer.Verify(f => f.AnalyzeFailureAsync(
            It.IsAny<NeuronScenario>(), It.IsAny<ScenarioResult>(),
            It.IsAny<string>(), It.IsAny<ProliferationContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCycle_WithDeduplicator_DeduplicatesChildren()
    {
        NeuronScenario scenario = new()
        {
            Id = "s1",
            SeedRuleCode = "TEST",
            ScenarioName = "Parent scenario",
            ProliferationReason = "unit test",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _store.Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationStats { TotalScenarios = 1 });
        _store.Setup(s => s.GetPendingScenariosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { scenario });
        _store.Setup(s => s.GetScenariosBySeedAsync("TEST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NeuronScenario> { scenario });

        _executor.Setup(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScenarioResult
            {
                ScenarioId = "s1",
                IsSuccess = true,
                MatchesExpectation = true,
                Duration = TimeSpan.FromMilliseconds(100),
                ExecutedAt = DateTimeOffset.UtcNow
            });

        NeuronScenario child = new()
        {
            Id = "child-1",
            SeedRuleCode = "TEST",
            ScenarioName = "Child",
            ProliferationReason = "generated",
            Status = ScenarioStatus.Pending,
            GenerationDepth = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _brain.Setup(b => b.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<JsonElement?>(), It.IsAny<JsonElement?>(),
                It.IsAny<ProliferationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProliferationPlan
            {
                SeedRuleCode = "TEST",
                AiModelUsed = "test",
                Scenarios = [child]
            });

        // Deduplicator returns the child (not filtered)
        _deduplicator.Setup(d => d.Deduplicate(
                It.IsAny<IReadOnlyList<NeuronScenario>>(),
                It.IsAny<IReadOnlyList<NeuronScenario>>()))
            .Returns<IReadOnlyList<NeuronScenario>, IReadOnlyList<NeuronScenario>>((c, _) => c);

        ProliferationWorker worker = CreateWorker(dedup: _deduplicator.Object);
        await worker.RunCycleAsync(CancellationToken.None);

        _deduplicator.Verify(d => d.Deduplicate(
            It.IsAny<IReadOnlyList<NeuronScenario>>(),
            It.IsAny<IReadOnlyList<NeuronScenario>>()), Times.Once);
    }
}
