namespace Muonroi.Experience.Runtime.Tests;

public sealed class FileExperienceStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileExperienceStore _store;

    public FileExperienceStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _store = CreateStore(new ExperienceStoreOptions
        {
            FileDirectoryPath = _tempDir,
            StoreType = ExperienceStoreType.File
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Test 1: StoreAsync Tier 2 entry persists to selfqa.json
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StoreAsync_Tier2Entry_PersistsToSelfQAFile()
    {
        NeuronExperience entry = MakeSelfQA();

        bool result = await _store.StoreAsync(entry);

        result.Should().BeTrue();
        string filePath = Path.Combine(_tempDir, "selfqa.json");
        System.IO.File.Exists(filePath).Should().BeTrue();

        string json = await System.IO.File.ReadAllTextAsync(filePath);
        var stored = JsonSerializer.Deserialize<List<NeuronExperience>>(json);
        stored.Should().NotBeNull();
        stored!.Should().ContainSingle(e => e.Id == entry.Id);
    }

    // -------------------------------------------------------------------------
    // Test 2: StoreAsync Tier 0 budget exceeded returns false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StoreAsync_Tier0BudgetExceeded_ReturnsFalse()
    {
        // PrincipleBudget = 10 tokens — very small so even a tiny entry fills it.
        var tightStore = CreateStore(new ExperienceStoreOptions
        {
            FileDirectoryPath = _tempDir + "_tight",
            Budget = new ExperienceBudgetConfig { PrincipleBudget = 10 }
        });

        // Fill budget with first entry (~10-token text).
        var filler = MakePrinciple("AB"); // tiny — fills 10-token budget quickly
        bool first = await tightStore.StoreAsync(filler);
        // Not guaranteed to succeed with PrincipleBudget=10, but keep trying until budget used.
        _ = first;

        // Store an entry large enough to overflow any tiny budget.
        var bigEntry = MakePrinciple(new string('X', 200)); // ~50 tokens
        bool result = await tightStore.StoreAsync(bigEntry);

        // At some point the budget must be exceeded.
        // The test verifies the false path exists; fill until it triggers.
        // But with budget=10 and 200-char entry = 50 tokens, first attempt already overflows.
        result.Should().BeFalse();

        Directory.Delete(_tempDir + "_tight", recursive: true);
    }

    // -------------------------------------------------------------------------
    // Test 3: StoreAsync Tier 3 always succeeds (no budget limit)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StoreAsync_Tier3_AlwaysSucceeds_NoBudgetLimit()
    {
        var tasks = Enumerable.Range(0, 20)
            .Select(i => _store.StoreAsync(MakeRaw($"raw-{i}")))
            .ToList();

        bool[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    // -------------------------------------------------------------------------
    // Test 4: FindRelevantAsync keyword match
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindRelevantAsync_KeywordMatch_ReturnsMatchingEntries()
    {
        var retryEntry = MakeEntry(ExperienceTier.SelfQA, trigger: "retry loop agent", question: "What is a retry?", solution: "Check idempotency");
        var unrelatedEntry = MakeEntry(ExperienceTier.Behavioral, trigger: "database query", question: "How to join?", solution: "Use INNER JOIN");
        var anotherEntry = MakeEntry(ExperienceTier.Principle, trigger: "caching strategy", question: "When to cache?", solution: "Cache slow reads");

        await _store.StoreAsync(retryEntry);
        await _store.StoreAsync(unrelatedEntry);
        await _store.StoreAsync(anotherEntry);

        IEnumerable<ExperienceSearchResult> results = await _store.FindRelevantAsync("retry loop");

        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.Experience.Id == retryEntry.Id);
        results.First().RelevanceScore.Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // Test 5: Self-QA round-trip preserves all fields (STO-05)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SelfQA_RoundTrip_PreservesAllFields()
    {
        var original = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = "missing null check",
            Question = "How to fix?",
            Reasoning = ["Step 1", "Step 2"],
            Solution = "Read first",
            Tier = ExperienceTier.SelfQA,
            Confidence = 0.5f,
            CreatedFrom = "session-123",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _store.StoreAsync(original);

        // Read back directly from file.
        string filePath = Path.Combine(_tempDir, "selfqa.json");
        string json = await System.IO.File.ReadAllTextAsync(filePath);
        var entries = JsonSerializer.Deserialize<List<NeuronExperience>>(json);

        entries.Should().NotBeNull();
        NeuronExperience? loaded = entries!.FirstOrDefault(e => e.Id == original.Id);
        loaded.Should().NotBeNull();
        loaded!.Question.Should().Be("How to fix?");
        loaded.Reasoning.Should().Equal("Step 1", "Step 2");
        loaded.Solution.Should().Be("Read first");
        loaded.CreatedFrom.Should().Be("session-123");
    }

    // -------------------------------------------------------------------------
    // Test 6: PromoteAsync Tier 2 -> Tier 1 moves entry
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PromoteAsync_Tier2ToTier1_MovesEntry()
    {
        NeuronExperience entry = MakeSelfQA();
        await _store.StoreAsync(entry);

        NeuronExperience promoted = await _store.PromoteAsync(entry);

        promoted.Tier.Should().Be(ExperienceTier.Behavioral);

        // Verify no longer in Tier 2 file.
        string selfQaFile = Path.Combine(_tempDir, "selfqa.json");
        if (System.IO.File.Exists(selfQaFile))
        {
            string json2 = await System.IO.File.ReadAllTextAsync(selfQaFile);
            var tier2Entries = JsonSerializer.Deserialize<List<NeuronExperience>>(json2);
            tier2Entries!.Should().NotContain(e => e.Id == entry.Id);
        }

        // Verify in Tier 1 file.
        string behavioralFile = Path.Combine(_tempDir, "behavioral.json");
        System.IO.File.Exists(behavioralFile).Should().BeTrue();
        string json1 = await System.IO.File.ReadAllTextAsync(behavioralFile);
        var tier1Entries = JsonSerializer.Deserialize<List<NeuronExperience>>(json1);
        tier1Entries!.Should().Contain(e => e.Id == entry.Id);
    }

    // -------------------------------------------------------------------------
    // Test 7: Concurrent writes do not lose data
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StoreAsync_ConcurrentWrites_NoDataLoss()
    {
        int count = 10;
        var entries = Enumerable.Range(0, count)
            .Select(i => MakeEntry(ExperienceTier.SelfQA, trigger: $"trigger-{i}", question: $"q-{i}", solution: $"s-{i}"))
            .ToList();

        bool[] results = await Task.WhenAll(entries.Select(e => _store.StoreAsync(e)));

        results.Should().AllSatisfy(r => r.Should().BeTrue());

        string filePath = Path.Combine(_tempDir, "selfqa.json");
        string json = await System.IO.File.ReadAllTextAsync(filePath);
        var stored = JsonSerializer.Deserialize<List<NeuronExperience>>(json);

        stored.Should().HaveCount(count);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static FileExperienceStore CreateStore(ExperienceStoreOptions opts)
        => new(Options.Create(opts));

    private static NeuronExperience MakeSelfQA(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = "test trigger",
        Question = "test question?",
        Reasoning = ["reason 1"],
        Solution = "test solution",
        Tier = ExperienceTier.SelfQA,
        Confidence = 0.5f,
        CreatedFrom = "test-session",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static NeuronExperience MakePrinciple(string content, string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = content,
        Question = content,
        Reasoning = [content],
        Solution = content,
        Tier = ExperienceTier.Principle,
        Confidence = 0.5f,
        CreatedFrom = "test-session",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static NeuronExperience MakeRaw(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Trigger = "raw trigger",
        Question = "raw question?",
        Reasoning = ["reason"],
        Solution = "raw solution",
        Tier = ExperienceTier.RawTrajectory,
        Confidence = 0.4f,
        CreatedFrom = "test-session",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static NeuronExperience MakeEntry(ExperienceTier tier, string trigger, string question, string solution) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Trigger = trigger,
        Question = question,
        Reasoning = ["step"],
        Solution = solution,
        Tier = tier,
        Confidence = 0.5f,
        CreatedFrom = "test-session",
        CreatedAt = DateTimeOffset.UtcNow
    };
}
