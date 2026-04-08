using FluentAssertions;
using Microsoft.Extensions.Options;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime;
using Muonroi.Experience.Runtime.File;
using Xunit;

namespace Muonroi.Experience.Tests;

[Trait("Category", "Store")]
public sealed class StoreIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly FileExperienceStore _store;

    public StoreIntegrationTests()
    {
        _store = new FileExperienceStore(Options.Create(new ExperienceStoreOptions
        {
            FileDirectoryPath = _tempDir,
            StoreType = ExperienceStoreType.File
        }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static NeuronExperience MakeEntry(
        ExperienceTier tier = ExperienceTier.SelfQA,
        string trigger = "retry loop agent",
        string id = null!)
    {
        return new NeuronExperience
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Trigger = trigger,
            Question = "Why does this fail?",
            Reasoning = ["step1", "step2"],
            Solution = "Read the error message first",
            Tier = tier,
            Confidence = 0.6f,
            CreatedFrom = "test-session",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task STO01_StoreAsync_RoutesByTier_PrincipleAndSelfQA()
    {
        var principle = MakeEntry(ExperienceTier.Principle);
        var selfQa = MakeEntry(ExperienceTier.SelfQA);

        var r1 = await _store.StoreAsync(principle);
        var r2 = await _store.StoreAsync(selfQa);

        r1.Should().BeTrue("Principle entry should be stored");
        r2.Should().BeTrue("SelfQA entry should be stored");

        var principles = await _store.FindAllInTierAsync(ExperienceTier.Principle);
        var selfQas = await _store.FindAllInTierAsync(ExperienceTier.SelfQA);
        principles.Should().ContainSingle(e => e.Id == principle.Id);
        selfQas.Should().ContainSingle(e => e.Id == selfQa.Id);
    }

    [Fact]
    public async Task STO02_BudgetEnforcement_RejectOverBudgetEntry()
    {
        var store = new FileExperienceStore(Options.Create(new ExperienceStoreOptions
        {
            FileDirectoryPath = _tempDir,
            StoreType = ExperienceStoreType.File,
            Budget = new ExperienceBudgetConfig { PrincipleBudget = 10 }
        }));

        var bigEntry = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = new string('x', 200),
            Question = "Over budget question",
            Reasoning = ["long reasoning step that exceeds budget"],
            Solution = "Over budget solution text here",
            Tier = ExperienceTier.Principle,
            Confidence = 0.9f,
            CreatedFrom = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await store.StoreAsync(bigEntry);
        result.Should().BeFalse("entry exceeds PrincipleBudget of 10 tokens");
    }

    [Fact]
    public async Task STO03_FindRelevantAsync_ReturnsMatchingEntries()
    {
        var entry = MakeEntry(trigger: "retry loop agent compilation error");
        await _store.StoreAsync(entry);

        var results = (await _store.FindRelevantAsync("retry loop")).ToList();

        results.Should().NotBeEmpty("keyword 'retry loop' appears in stored entry's Trigger");
        results[0].RelevanceScore.Should().BeGreaterThan(0f);
    }

    [Fact]
    public async Task STO04_FileStore_RoundTrip_AllFieldsPreserved()
    {
        var original = new NeuronExperience
        {
            Id = "roundtrip-1",
            Trigger = "retry loop agent",
            Question = "Why does this fail?",
            Reasoning = ["step1", "step2"],
            Solution = "Read error first",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.75f,
            CreatedFrom = "session-42",
            CreatedAt = new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero)
        };

        await _store.StoreAsync(original);

        var retrieved = (await _store.FindAllInTierAsync(ExperienceTier.SelfQA))
            .FirstOrDefault(e => e.Id == "roundtrip-1");

        retrieved.Should().NotBeNull();
        retrieved!.Trigger.Should().Be("retry loop agent");
        retrieved.Question.Should().Be("Why does this fail?");
        retrieved.Reasoning.Should().BeEquivalentTo(new[] { "step1", "step2" });
        retrieved.Solution.Should().Be("Read error first");
        retrieved.Confidence.Should().Be(0.75f);
        retrieved.CreatedFrom.Should().Be("session-42");
    }
}
