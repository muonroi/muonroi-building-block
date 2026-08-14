// ── Synthetic 20-entry session log (3 retry loops, 1 user correction) ──────────
const string SessionLog = """
    {"role":"assistant","content":[{"type":"tool_use","id":"t1","name":"Edit","input":{"file_path":"Service.cs","old_string":"x","new_string":"y"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t1","content":"error: compilation failed"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t2","name":"Edit","input":{"file_path":"Service.cs","old_string":"x","new_string":"y"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t2","content":"error: compilation failed"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t3","name":"Edit","input":{"file_path":"Service.cs","old_string":"x","new_string":"y"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t3","content":"error: compilation failed"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t4","name":"Edit","input":{"file_path":"Service.cs","old_string":"a","new_string":"b"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t4","content":"ok"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t5","name":"Write","input":{"file_path":"Config.cs","content":"class C {}"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t5","content":"ok"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t6","name":"Bash","input":{"command":"dotnet build"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t6","content":"Build succeeded"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t7","name":"Bash","input":{"command":"dotnet test"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t7","content":"Passed: 5 Failed: 0"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t8","name":"Edit","input":{"file_path":"Repo.cs","old_string":"null","new_string":"string.Empty"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t8","content":"error: null ref"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t9","name":"Edit","input":{"file_path":"Repo.cs","old_string":"null","new_string":"string.Empty"}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t9","content":"error: null ref"}]}
    {"role":"assistant","content":[{"type":"tool_use","id":"t10","name":"Edit","input":{"file_path":"Repo.cs","old_string":"null","new_string":""}}]}
    {"role":"user","content":[{"type":"tool_result","tool_use_id":"t10","content":"ok"}]}
    """;

// ── Step 1: Setup ─────────────────────────────────────────────────────────────
string tempDir = Path.Combine(Path.GetTempPath(), "experience-sample-" + Guid.NewGuid());
Console.WriteLine("Step 1 — Setup: temp store at " + tempDir);

// ── Step 2: MistakeDetector ───────────────────────────────────────────────────
var detector = new MistakeDetector();
var signals = (await detector.DetectAsync(SessionLog, default)).ToList();
Console.WriteLine($"Step 2 — MistakeDetector: {signals.Count} signals detected");

// ── Step 3: Mock brain (no real LLM calls) ────────────────────────────────────
int callCount = 0;
var brainMock = new Mock<IExperienceBrain>();
brainMock
    .Setup(b => b.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(() =>
    {
        callCount++;
        return new NeuronExperience[]
        {
            new NeuronExperience
            {
                Id = Guid.NewGuid().ToString(),
                Trigger = $"retry-loop-{callCount}",
                Question = "Why does repeated Edit fail?",
                Reasoning = new[] { "Check compilation errors before retrying", "Read error message first" },
                Solution = "Read compiler output and fix root cause before repeating the same edit",
                Tier = ExperienceTier.SelfQA,
                Confidence = 0.6f,
                CreatedFrom = "sample-session",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };
    });
brainMock
    .Setup(b => b.AbstractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new NeuronExperience
    {
        Id = Guid.NewGuid().ToString(),
        Trigger = "repeated-edit-failure",
        Question = "Why does repeating the same Edit action fail multiple times?",
        Reasoning = new[] { "Error output not read", "Root cause not diagnosed before retry" },
        Solution = "Always read compiler/tool error output before repeating the same edit action",
        Principle = "Principle: Always read error output before repeating the same edit action",
        Tier = ExperienceTier.Principle,
        Confidence = 0.9f,
        CreatedFrom = "sample-cluster",
        CreatedAt = DateTimeOffset.UtcNow
    });
Console.WriteLine("Step 3 — Brain mock configured");

// ── Step 4: Store + Extraction pipeline ──────────────────────────────────────
var storeOpts = new ExperienceStoreOptions
{
    FileDirectoryPath = tempDir,
    StoreType = ExperienceStoreType.File
};
var store = new FileExperienceStore(
    Options.Create(storeOpts),
    log: null,
    brain: brainMock.Object);

var pipeline = new ExperienceExtractionPipeline(
    detector,
    brainMock.Object,
    store,
    Options.Create(storeOpts));

// ExtractQAAsync internally runs DetectAsync + brain per signal + dedup + store
var extracted = (await pipeline.ExtractQAAsync(SessionLog, default)).ToList();
Console.WriteLine($"Step 4 — Store: {extracted.Count} entries stored");

// ── Step 5: Interception ──────────────────────────────────────────────────────
var interceptor = new DefaultExperienceInterceptor(store);
string context = await interceptor.InterceptAsync("Edit", "Service.cs compilation error", default);
Console.WriteLine($"Step 5 — Interception: context injected = {!string.IsNullOrEmpty(context)}");

// ── Step 6: Tier distribution ─────────────────────────────────────────────────
var principles  = (await store.FindAllInTierAsync(ExperienceTier.Principle, default)).ToList();
var behavioral  = (await store.FindAllInTierAsync(ExperienceTier.Behavioral, default)).ToList();
var selfqa      = (await store.FindAllInTierAsync(ExperienceTier.SelfQA, default)).ToList();
int totalBefore = selfqa.Count + behavioral.Count + principles.Count;
Console.WriteLine($"Step 6 — Tiers: Principle={principles.Count}, Behavioral={behavioral.Count}, SelfQA={selfqa.Count}");

// ── Step 7: Evolution (cluster SelfQA entries into Principle) ─────────────────
if (selfqa.Count >= 2)
{
    // ClusterAndAbstractAsync groups similar entries and stores a new Principle
    await store.ClusterAndAbstractAsync(selfqa, default);
    // Delete the source SelfQA entries (orchestrator normally does this)
    foreach (var e in selfqa)
        await store.DeleteAsync(e.Id, default);
}

var principlesAfter = (await store.FindAllInTierAsync(ExperienceTier.Principle, default)).ToList();
var behavioralAfter = (await store.FindAllInTierAsync(ExperienceTier.Behavioral, default)).ToList();
int totalAfter = principlesAfter.Count + behavioralAfter.Count;
Console.WriteLine("Step 7 — Evolution complete");

// ── Summary ───────────────────────────────────────────────────────────────────
Console.WriteLine("─── Experience Engine Sample — Summary ──────────────────────────────");
Console.WriteLine($"Signals detected  : {signals.Count}");
Console.WriteLine($"Entries extracted : {extracted.Count}");
Console.WriteLine($"Tier distribution : Principle={principlesAfter.Count}, Behavioral={behavioralAfter.Count}, SelfQA=0");
double ratio = totalBefore > 0 ? (double)totalBefore / Math.Max(totalAfter, 1) : 1.0;
Console.WriteLine($"Compression ratio : {ratio:F2}x");
Console.WriteLine("────────────────────────────────────────────────────────────────────");

// Cleanup
if (Directory.Exists(tempDir))
    Directory.Delete(tempDir, recursive: true);
