using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime.Archive;
using Muonroi.Experience.Runtime.Evolution;
using Muonroi.Experience.Runtime.File;
using Xunit;

namespace Muonroi.Experience.Runtime.Tests;

/// <summary>
/// Tests for EVO-05 — archival sweep in ExperienceEvolutionOrchestrator.
/// Verifies stale entries (HitCount=0, age >90d) are archived and removed from active store.
/// </summary>
[Trait("Category", "Evolution")]
public sealed class ArchivalTests : IDisposable
{
    private readonly string _tempDir;

    public ArchivalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "archival-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private FileExperienceStore MakeStore(ExperienceBudgetConfig? budget = null)
    {
        var opts = Options.Create(new ExperienceStoreOptions
        {
            FileDirectoryPath = _tempDir,
            StoreType = ExperienceStoreType.File,
            Budget = budget ?? new ExperienceBudgetConfig()
        });
        return new FileExperienceStore(opts);
    }

    private ExperienceEvolutionOrchestrator MakeOrchestrator(
        FileExperienceStore store,
        IExperienceArchive archive,
        int archivalDaysThreshold = 90,
        Mock<IExperienceBrain>? brainMock = null)
    {
        var brain = brainMock?.Object ?? Mock.Of<IExperienceBrain>();
        var budget = new ExperienceBudgetConfig { ArchivalDaysThreshold = archivalDaysThreshold };
        var opts = new EvolutionOptions { MinClusterSize = 3, ClusterSimilarityThreshold = 0.7f };
        return new ExperienceEvolutionOrchestrator(store, brain, archive, opts, budget);
    }

    private static NeuronExperience MakeEntry(
        int hitCount,
        DateTimeOffset createdAt,
        string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = "test trigger phrase",
        Question = "test question?",
        Reasoning = [],
        Solution = "test solution",
        Tier = ExperienceTier.SelfQA,
        HitCount = hitCount,
        Confidence = 0.5f,
        CreatedFrom = "test",
        CreatedAt = createdAt
    };

    // -------------------------------------------------------------------------
    // Test 1 (EVO-05): Stale entry (HitCount=0, age=91d) → archived and removed from store
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunEvolutionAsync_StaleEntry_IsArchivedAndRemovedFromStore()
    {
        var store = MakeStore();
        var archiveMock = new Mock<IExperienceArchive>();

        NeuronExperience stale = MakeEntry(
            hitCount: 0,
            createdAt: DateTimeOffset.UtcNow.AddDays(-91));

        await store.StoreAsync(stale);

        var orchestrator = MakeOrchestrator(store, archiveMock.Object);
        await orchestrator.RunEvolutionAsync();

        // Archive called once with the stale entry
        archiveMock.Verify(a => a.ArchiveAsync(
            It.Is<NeuronExperience>(e => e.Id == stale.Id),
            It.IsAny<CancellationToken>()), Times.Once);

        // Entry removed from active store
        IEnumerable<NeuronExperience> remaining = await store.FindAllInTierAsync(ExperienceTier.SelfQA);
        remaining.Should().BeEmpty("stale entry must be removed from active store after archival");
    }

    // -------------------------------------------------------------------------
    // Test 2: Entry with HitCount=1, age>90d → NOT archived (HitCount check)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunEvolutionAsync_EntryWithHits_NotArchived()
    {
        var store = MakeStore();
        var archiveMock = new Mock<IExperienceArchive>();

        NeuronExperience active = MakeEntry(
            hitCount: 1,
            createdAt: DateTimeOffset.UtcNow.AddDays(-91));

        await store.StoreAsync(active);

        var orchestrator = MakeOrchestrator(store, archiveMock.Object);
        await orchestrator.RunEvolutionAsync();

        // Archive NOT called
        archiveMock.Verify(a => a.ArchiveAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()), Times.Never);

        // Entry still in store
        IEnumerable<NeuronExperience> remaining = await store.FindAllInTierAsync(ExperienceTier.SelfQA);
        remaining.Should().ContainSingle(e => e.Id == active.Id);
    }

    // -------------------------------------------------------------------------
    // Test 3: Entry with HitCount=0, age=89d → NOT archived (age check)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunEvolutionAsync_TooYoungEntry_NotArchived()
    {
        var store = MakeStore();
        var archiveMock = new Mock<IExperienceArchive>();

        NeuronExperience young = MakeEntry(
            hitCount: 0,
            createdAt: DateTimeOffset.UtcNow.AddDays(-89));

        await store.StoreAsync(young);

        var orchestrator = MakeOrchestrator(store, archiveMock.Object);
        await orchestrator.RunEvolutionAsync();

        // Archive NOT called
        archiveMock.Verify(a => a.ArchiveAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()), Times.Never);

        // Entry still in store
        IEnumerable<NeuronExperience> remaining = await store.FindAllInTierAsync(ExperienceTier.SelfQA);
        remaining.Should().ContainSingle(e => e.Id == young.Id);
    }

    // -------------------------------------------------------------------------
    // Test 4: EvolutionReport.ArchivedCount matches number of archived entries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunEvolutionAsync_ArchivedCount_ReflectsActualArchivals()
    {
        var store = MakeStore();
        var archiveMock = new Mock<IExperienceArchive>();

        // Two stale entries
        await store.StoreAsync(MakeEntry(hitCount: 0, createdAt: DateTimeOffset.UtcNow.AddDays(-100)));
        await store.StoreAsync(MakeEntry(hitCount: 0, createdAt: DateTimeOffset.UtcNow.AddDays(-95)));
        // One active entry
        await store.StoreAsync(MakeEntry(hitCount: 1, createdAt: DateTimeOffset.UtcNow.AddDays(-100)));

        var orchestrator = MakeOrchestrator(store, archiveMock.Object);
        EvolutionReport report = await orchestrator.RunEvolutionAsync();

        report.ArchivedCount.Should().Be(2);
    }
}
