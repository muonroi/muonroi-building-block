using FluentAssertions;
using Microsoft.Extensions.Options;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime.File;
using Xunit;

namespace Muonroi.Experience.Runtime.Tests;

/// <summary>
/// Tests for EVO-01 (promotion sweep) and EVO-02 (demotion) behavior via FileExperienceStore.
/// Uses a temp-directory store with no Qdrant dependency — pure in-process validation.
/// </summary>
[Trait("Category", "Evolution")]
public sealed class EvolutionOrchestratorTests : IDisposable
{
    private readonly string _tempDir;

    public EvolutionOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "evo-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private FileExperienceStore MakeStore(string? dir = null)
    {
        string path = dir ?? _tempDir;
        var opts = Options.Create(new ExperienceStoreOptions
        {
            FileDirectoryPath = path,
            StoreType = ExperienceStoreType.File,
            Budget = new ExperienceBudgetConfig()
        });
        return new FileExperienceStore(opts);
    }

    private static NeuronExperience MakeEntry(
        ExperienceTier tier,
        int hitCount = 0,
        string? trigger = null,
        string? solution = null,
        string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = trigger ?? "test trigger",
        Question = "test question?",
        Reasoning = ["step one"],
        Solution = solution ?? "test solution",
        Tier = tier,
        HitCount = hitCount,
        Confidence = 0.7f,
        CreatedFrom = "test-session",
        CreatedAt = DateTimeOffset.UtcNow
    };

    // -------------------------------------------------------------------------
    // Test 1: FindAllInTierAsync returns entries for the requested tier only
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindAllInTierAsync_ReturnsTierEntries()
    {
        var store = MakeStore();

        var selfQa1 = MakeEntry(ExperienceTier.SelfQA);
        var selfQa2 = MakeEntry(ExperienceTier.SelfQA);
        var behavioral = MakeEntry(ExperienceTier.Behavioral);

        await store.StoreAsync(selfQa1);
        await store.StoreAsync(selfQa2);
        await store.StoreAsync(behavioral);

        IEnumerable<NeuronExperience> result = await store.FindAllInTierAsync(ExperienceTier.SelfQA);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.Tier.Should().Be(ExperienceTier.SelfQA));
    }

    // -------------------------------------------------------------------------
    // Test 2: DeleteAsync removes entry — FindAllInTierAsync returns empty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var store = MakeStore();
        var entry = MakeEntry(ExperienceTier.SelfQA);
        await store.StoreAsync(entry);

        await store.DeleteAsync(entry.Id);

        IEnumerable<NeuronExperience> remaining = await store.FindAllInTierAsync(ExperienceTier.SelfQA);
        remaining.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Test 3 (EVO-01): PromoteAsync moves entry to higher tier
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PromoteAsync_MovesEntryToHigherTier()
    {
        var store = MakeStore();
        var entry = MakeEntry(ExperienceTier.SelfQA, hitCount: 3);
        await store.StoreAsync(entry);

        // Orchestrator pre-formats before calling PromoteAsync.
        NeuronExperience formatted = entry with
        {
            Solution = $"WHEN {entry.Trigger} \u2192 {entry.Solution}",
            Reasoning = []
        };

        NeuronExperience promoted = await store.PromoteAsync(formatted);

        promoted.Tier.Should().Be(ExperienceTier.Behavioral);
        IEnumerable<NeuronExperience> tier2 = await store.FindAllInTierAsync(ExperienceTier.SelfQA);
        tier2.Should().BeEmpty("source Tier 2 entry must be removed after promotion");
        IEnumerable<NeuronExperience> tier1 = await store.FindAllInTierAsync(ExperienceTier.Behavioral);
        tier1.Should().ContainSingle(e => e.Id == entry.Id);
    }

    // -------------------------------------------------------------------------
    // Test 4 (EVO-02): DemoteAsync moves entry to lower tier and resets HitCount
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DemoteAsync_MovesEntryToLowerTier_ResetsHitCount()
    {
        var store = MakeStore();
        var entry = MakeEntry(ExperienceTier.Behavioral, hitCount: 5);
        await store.StoreAsync(entry);

        NeuronExperience demoted = await store.DemoteAsync(entry);

        demoted.Tier.Should().Be(ExperienceTier.SelfQA);
        demoted.HitCount.Should().Be(0);
        IEnumerable<NeuronExperience> tier1 = await store.FindAllInTierAsync(ExperienceTier.Behavioral);
        tier1.Should().BeEmpty("source Tier 1 entry must be removed after demotion");
        IEnumerable<NeuronExperience> tier2 = await store.FindAllInTierAsync(ExperienceTier.SelfQA);
        tier2.Should().ContainSingle(e => e.Id == entry.Id);
    }

    // -------------------------------------------------------------------------
    // Test 5 (EVO-01 format): Solution starts with "WHEN" in promoted entry
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PromoteAsync_WithHitCountAtThreshold_BehavioralFormatPreserved()
    {
        var store = MakeStore();
        const string trigger = "test";
        var entry = MakeEntry(ExperienceTier.SelfQA, hitCount: 3, trigger: trigger, solution: "fix it");
        await store.StoreAsync(entry);

        // Orchestrator pre-formats the solution before calling PromoteAsync.
        NeuronExperience formatted = entry with
        {
            Solution = $"WHEN {trigger} \u2192 fix it",
            Reasoning = []
        };

        NeuronExperience promoted = await store.PromoteAsync(formatted);

        promoted.Solution.Should().StartWith("WHEN");
        promoted.Tier.Should().Be(ExperienceTier.Behavioral);
    }

    // -------------------------------------------------------------------------
    // Test 6: ClusterAndAbstractAsync with no brain throws NotSupportedException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Brain_Null_ThrowsOnClusterAndAbstractAsync()
    {
        var store = MakeStore(); // no brain registered

        Func<Task> act = async () => await store.ClusterAndAbstractAsync(
            [MakeEntry(ExperienceTier.SelfQA)]);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*IExperienceBrain not registered*");
    }
}
