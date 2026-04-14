using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime;
using Muonroi.Experience.Runtime.Extraction;
using Xunit;

namespace Muonroi.Experience.Runtime.Tests;

[Trait("Category", "Extraction")]
public sealed class ExperienceExtractionPipelineTests
{
    // -------------------------------------------------------------------------
    // Fixtures
    // -------------------------------------------------------------------------

    // 3 identical Edit tool_use entries targeting same file -> retry_loop signal
    private const string RetryLoopJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs","action":"edit","content":"fix1"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"1","content":"ok"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs","action":"edit","content":"fix2"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"2","content":"ok"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs","action":"edit","content":"fix3"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"3","content":"ok"}]}
        """;

    // user text after assistant tool_use -> user_correction signal
    private const string UserCorrectionJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Baz.cs","action":"edit","content":"wrong"}}]}
        {"role":"user","content":[{"type":"text","text":"That is wrong, please revert and try differently."}]}
        """;

    // git revert command -> git_revert signal
    private const string GitRevertJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Bash","input":{"command":"git revert HEAD~1 --no-edit"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"1","content":"[main abcd123] Revert previous commit"}]}
        """;

    // test red/green -> test_red_green signal
    private const string TestRedGreenJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Bash","input":{"command":"dotnet test"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"1","content":"FAILED: 2 tests failed\nError in TestFoo\n"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/TestTarget.cs","action":"edit","content":"fixed"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"2","content":"ok"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Bash","input":{"command":"dotnet test"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"3","content":"passed: 10 tests passed\nAll tests passed.\n"}]}
        """;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NeuronExperience MakeEntry(string trigger, int hitCount = 0) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Trigger = trigger,
        Question = "What went wrong?",
        Reasoning = ["Because the file wasn't read first"],
        Solution = "Always read before editing",
        Confidence = 0.5f,
        HitCount = hitCount,
        Tier = ExperienceTier.SelfQA,
        CreatedFrom = "test-session",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static ExperienceExtractionPipeline BuildPipeline(
        Mock<IExperienceBrain> brainMock,
        Mock<IExperienceStore> storeMock,
        ExperienceStoreOptions? storeOpts = null)
    {
        var opts = storeOpts ?? new ExperienceStoreOptions();
        var optionsMock = new Mock<IOptions<ExperienceStoreOptions>>();
        optionsMock.Setup(o => o.Value).Returns(opts);

        return new ExperienceExtractionPipeline(
            new MistakeDetector(),
            brainMock.Object,
            storeMock.Object,
            optionsMock.Object);
    }

    // -------------------------------------------------------------------------
    // Test 1: retry_loop signal -> brain extracts 1 entry -> no near-duplicate -> entry stored with Confidence 0.6 and Tier SelfQA
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_RetryLoopSignal_StoresEntryWithConfidence06AndTierSelfQA()
    {
        // Arrange
        var entry = MakeEntry("editing same file repeatedly");
        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([entry]);

        var storeMock = new Mock<IExperienceStore>();
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var pipeline = BuildPipeline(brainMock, storeMock);

        // Act
        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(RetryLoopJsonl);

        // Assert
        result.Should().HaveCount(1);
        NeuronExperience stored = result.Single();
        stored.Confidence.Should().Be(0.6f);
        stored.Tier.Should().Be(ExperienceTier.SelfQA);
        storeMock.Verify(s => s.StoreAsync(It.Is<NeuronExperience>(e => e.Confidence == 0.6f && e.Tier == ExperienceTier.SelfQA), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 2: Dedup — RelevanceScore 0.9 (> 0.85) -> StoreAsync called with hitCount+1, new entry NOT stored separately
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_NearDuplicate_IncrementsHitCountOnExistingAndSkipsNew()
    {
        // Arrange
        var existingEntry = MakeEntry("existing trigger", hitCount: 2);
        var newEntry = MakeEntry("editing same file repeatedly");

        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([newEntry]);

        var storeMock = new Mock<IExperienceStore>();
        // High relevance -> duplicate
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new ExperienceSearchResult(existingEntry, 0.9f)]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var pipeline = BuildPipeline(brainMock, storeMock);

        // Act
        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(RetryLoopJsonl);

        // Assert: new entry NOT in result (it's a duplicate)
        result.Should().BeEmpty();
        // Existing entry's HitCount incremented to 3
        storeMock.Verify(
            s => s.StoreAsync(It.Is<NeuronExperience>(e => e.HitCount == 3), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 3: Dedup — RelevanceScore 0.7 (< 0.85) -> new entry IS stored
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_BelowDedupThreshold_StoresNewEntry()
    {
        // Arrange
        var existingEntry = MakeEntry("something else", hitCount: 1);
        var newEntry = MakeEntry("editing same file repeatedly");

        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([newEntry]);

        var storeMock = new Mock<IExperienceStore>();
        // Low relevance -> not a duplicate
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new ExperienceSearchResult(existingEntry, 0.7f)]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var pipeline = BuildPipeline(brainMock, storeMock);

        // Act
        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(RetryLoopJsonl);

        // Assert: new entry IS in result
        result.Should().HaveCount(1);
        // StoreAsync called with scored entry (Confidence = 0.6 for retry_loop, NOT with HitCount+1)
        storeMock.Verify(
            s => s.StoreAsync(It.Is<NeuronExperience>(e => e.Confidence == 0.6f), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 4: Confidence assignment per signal type
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_RetryLoop_Confidence06()
    {
        var entry = MakeEntry("trigger");
        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([entry]);
        var storeMock = new Mock<IExperienceStore>();
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        var pipeline = BuildPipeline(brainMock, storeMock);

        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(RetryLoopJsonl);

        result.Single().Confidence.Should().Be(0.6f);
    }

    [Fact]
    public async Task ExtractQAAsync_UserCorrection_Confidence06()
    {
        var entry = MakeEntry("trigger");
        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([entry]);
        var storeMock = new Mock<IExperienceStore>();
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        var pipeline = BuildPipeline(brainMock, storeMock);

        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(UserCorrectionJsonl);

        result.Single().Confidence.Should().Be(0.6f);
    }

    [Fact]
    public async Task ExtractQAAsync_TestRedGreen_Confidence06()
    {
        var entry = MakeEntry("trigger");
        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([entry]);
        var storeMock = new Mock<IExperienceStore>();
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        var pipeline = BuildPipeline(brainMock, storeMock);

        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(TestRedGreenJsonl);

        result.Single().Confidence.Should().Be(0.6f);
    }

    [Fact]
    public async Task ExtractQAAsync_GitRevert_Confidence04()
    {
        var entry = MakeEntry("trigger");
        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([entry]);
        var storeMock = new Mock<IExperienceStore>();
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        var pipeline = BuildPipeline(brainMock, storeMock);

        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(GitRevertJsonl);

        result.Single().Confidence.Should().Be(0.4f);
    }

    // -------------------------------------------------------------------------
    // Test 5: All entries have Tier = ExperienceTier.SelfQA
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_AllEntriesHaveTierSelfQA()
    {
        var entries = new[]
        {
            MakeEntry("trigger1"),
            MakeEntry("trigger2")
        };
        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(entries);
        var storeMock = new Mock<IExperienceStore>();
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        var pipeline = BuildPipeline(brainMock, storeMock);

        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(RetryLoopJsonl);

        result.Should().AllSatisfy(e => e.Tier.Should().Be(ExperienceTier.SelfQA));
    }

    // -------------------------------------------------------------------------
    // Test 6: Empty session log -> returns empty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_EmptySessionLog_ReturnsEmpty()
    {
        var brainMock = new Mock<IExperienceBrain>();
        var storeMock = new Mock<IExperienceStore>();
        var pipeline = BuildPipeline(brainMock, storeMock);

        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync("");

        result.Should().BeEmpty();
        brainMock.Verify(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        storeMock.Verify(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 7: Brain returns empty for a signal -> no store call
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_BrainReturnsEmpty_NoStoreCall()
    {
        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        var storeMock = new Mock<IExperienceStore>();
        var pipeline = BuildPipeline(brainMock, storeMock);

        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(RetryLoopJsonl);

        result.Should().BeEmpty();
        storeMock.Verify(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 8: Dedup match increments hitCount — verify StoreAsync called with existingEntry.HitCount + 1
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractQAAsync_DedupMatch_StoresExistingEntryWithIncrementedHitCount()
    {
        // Arrange: existing entry has HitCount = 5
        var existingEntry = MakeEntry("existing trigger", hitCount: 5);
        var newEntry = MakeEntry("new trigger");

        var brainMock = new Mock<IExperienceBrain>();
        brainMock.Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([newEntry]);

        var storeMock = new Mock<IExperienceStore>();
        storeMock.Setup(s => s.FindRelevantAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new ExperienceSearchResult(existingEntry, 0.95f)]);
        storeMock.Setup(s => s.StoreAsync(It.IsAny<NeuronExperience>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var pipeline = BuildPipeline(brainMock, storeMock);

        // Act
        IEnumerable<NeuronExperience> result = await pipeline.ExtractQAAsync(RetryLoopJsonl);

        // Assert: HitCount incremented from 5 to 6
        result.Should().BeEmpty();
        storeMock.Verify(
            s => s.StoreAsync(It.Is<NeuronExperience>(e => e.HitCount == 6), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
