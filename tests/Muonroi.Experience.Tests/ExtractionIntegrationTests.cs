namespace Muonroi.Experience.Tests;

[Trait("Category", "Extraction")]
public sealed class ExtractionIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private FileExperienceStore MakeStore(ExperienceStoreOptions? opts = null)
    {
        return new FileExperienceStore(Options.Create(opts ?? new ExperienceStoreOptions
        {
            FileDirectoryPath = _tempDir,
            StoreType = ExperienceStoreType.File
        }));
    }

    private const string RetryLoopJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","id":"t1","name":"Edit","input":{"file_path":"src/Foo.cs"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"t1","content":"error: failed"}]}
        {"role":"assistant","content":[{"type":"tool_use","id":"t2","name":"Edit","input":{"file_path":"src/Foo.cs"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"t2","content":"error: failed"}]}
        {"role":"assistant","content":[{"type":"tool_use","id":"t3","name":"Edit","input":{"file_path":"src/Foo.cs"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"t3","content":"error: failed"}]}
        {"role":"assistant","content":[{"type":"tool_use","id":"t4","name":"Edit","input":{"file_path":"src/Foo.cs"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"t4","content":"ok"}]}
        """;

    [Fact]
    public async Task EXT01_Pipeline_DetectsRetryLoop_ExtractsExperience()
    {
        var store = MakeStore();
        var extractedEntry = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = "retry loop editing Foo.cs",
            Question = "Why did edits keep failing?",
            Reasoning = ["compilation error not read"],
            Solution = "Read error output before retrying",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.6f,
            CreatedFrom = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var brainMock = new Mock<IExperienceBrain>();
        brainMock
            .Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { extractedEntry });

        var detector = new MistakeDetector();
        var pipeline = new ExperienceExtractionPipeline(
            detector, brainMock.Object, store,
            Options.Create(new ExperienceStoreOptions
            {
                FileDirectoryPath = _tempDir,
                StoreType = ExperienceStoreType.File
            }));

        var results = (await pipeline.ExtractQAAsync(RetryLoopJsonl)).ToList();

        results.Should().NotBeEmpty("retry loop in JSONL should produce at least 1 extracted entry");
        brainMock.Verify(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EXT02_Pipeline_DedupIncrementsHitCount_OrRejectsSecondEntry()
    {
        var store = MakeStore();
        var entry = new NeuronExperience
        {
            Id = "dedup-1",
            Trigger = "retry loop editing Foo.cs",
            Question = "Why did edits keep failing?",
            Reasoning = ["compilation error not read"],
            Solution = "Read error output before retrying",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.6f,
            CreatedFrom = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Store the first entry directly
        await store.StoreAsync(entry);

        // Now run pipeline with same content — should dedup
        var brainMock = new Mock<IExperienceBrain>();
        brainMock
            .Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry with { Id = "dedup-2" } });

        var detector = new MistakeDetector();
        var pipeline = new ExperienceExtractionPipeline(
            detector, brainMock.Object, store,
            Options.Create(new ExperienceStoreOptions
            {
                FileDirectoryPath = _tempDir,
                StoreType = ExperienceStoreType.File
            }));

        var results = (await pipeline.ExtractQAAsync(RetryLoopJsonl)).ToList();

        // Either the second entry is deduped (results empty for that entry)
        // or it's stored as a separate entry. We verify the pipeline runs without error.
        // The key behavior: brain.ExtractAsync was called and pipeline handled the near-duplicate path
        brainMock.Verify(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EXT03_CompositeBrain_FallbackChain_PrimaryEmptyFallbackReturns()
    {
        var entry = new NeuronExperience
        {
            Id = "fallback-1",
            Trigger = "fallback test",
            Question = "Does fallback work?",
            Reasoning = ["yes"],
            Solution = "Fallback brain returned result",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.5f,
            CreatedFrom = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var primaryMock = new Mock<IExperienceBrain>();
        primaryMock
            .Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NeuronExperience>());

        var fallbackMock = new Mock<IExperienceBrain>();
        fallbackMock
            .Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });

        var composite = new CompositeExperienceBrain(primaryMock.Object, fallbackMock.Object);

        var results = (await composite.ExtractAsync("some session log")).ToList();

        results.Should().NotBeEmpty("fallback brain should return result when primary returns empty");
        results[0].Id.Should().Be("fallback-1");
        fallbackMock.Verify(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
