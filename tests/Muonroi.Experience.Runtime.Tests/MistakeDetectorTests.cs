namespace Muonroi.Experience.Runtime.Tests;

[Trait("Category", "Extraction")]
public sealed class MistakeDetectorTests
{
    private readonly MistakeDetector _detector = new();

    // -------------------------------------------------------------------------
    // Fixtures (synthetic JSONL)
    // -------------------------------------------------------------------------

    // 3 identical Edit tool_use entries targeting same file -> retry_loop
    private const string RetryLoopJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs","action":"edit","content":"fix1"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"1","content":"ok"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs","action":"edit","content":"fix2"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"2","content":"ok"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs","action":"edit","content":"fix3"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"3","content":"ok"}]}
        """;

    // Only 2 identical tool calls (below threshold of 3)
    private const string TwoIdenticalCallsJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Bar.cs","action":"edit","content":"v1"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"1","content":"ok"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Bar.cs","action":"edit","content":"v2"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"2","content":"ok"}]}
        """;

    // user text (non-tool_result) immediately after assistant tool_use -> user_correction
    private const string UserCorrectionJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Baz.cs","action":"edit","content":"wrong"}}]}
        {"role":"user","content":[{"type":"text","text":"That is wrong, please revert and try differently."}]}
        """;

    // git revert: Bash tool with "revert" in command -> git_revert
    private const string GitRevertJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Bash","input":{"command":"git revert HEAD~1 --no-edit"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"1","content":"[main abcd123] Revert previous commit"}]}
        """;

    // test_red_green: Bash result with FAILED -> Edit -> Bash result with passed
    private const string TestRedGreenJsonl = """
        {"role":"assistant","content":[{"type":"tool_use","name":"Bash","input":{"command":"dotnet test"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"1","content":"FAILED: 2 tests failed\nError in TestFoo\n"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/TestTarget.cs","action":"edit","content":"fixed"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"2","content":"ok"}]}
        {"role":"assistant","content":[{"type":"tool_use","name":"Bash","input":{"command":"dotnet test"}}]}
        {"role":"user","content":[{"type":"tool_result","tool_use_id":"3","content":"passed: 10 tests passed\nAll tests passed.\n"}]}
        """;

    // -------------------------------------------------------------------------
    // Test 1: retry_loop detected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DetectAsync_RetryLoop_EmitsMistakeSignal()
    {
        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(RetryLoopJsonl);

        signals.Should().NotBeEmpty();
        signals.Should().Contain(s => s.SignalType == "retry_loop");
    }

    // -------------------------------------------------------------------------
    // Test 2: user_correction detected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DetectAsync_UserCorrection_EmitsMistakeSignal()
    {
        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(UserCorrectionJsonl);

        signals.Should().NotBeEmpty();
        signals.Should().Contain(s => s.SignalType == "user_correction");
    }

    // -------------------------------------------------------------------------
    // Test 3: git_revert detected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DetectAsync_GitRevert_EmitsMistakeSignal()
    {
        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(GitRevertJsonl);

        signals.Should().NotBeEmpty();
        signals.Should().Contain(s => s.SignalType == "git_revert");
    }

    // -------------------------------------------------------------------------
    // Test 4: test_red_green detected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DetectAsync_TestRedGreen_EmitsMistakeSignal()
    {
        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(TestRedGreenJsonl);

        signals.Should().NotBeEmpty();
        signals.Should().Contain(s => s.SignalType == "test_red_green");
    }

    // -------------------------------------------------------------------------
    // Test 5: Context is windowed (not the entire session log)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DetectAsync_Context_IsWindowedNotFullLog()
    {
        // Build a large JSONL that is clearly bigger than 30 lines
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            sb.AppendLine($"{{\"role\":\"assistant\",\"content\":[{{\"type\":\"tool_use\",\"name\":\"Read\",\"input\":{{\"file_path\":\"src/Padding{i}.cs\"}}}}]}}");
            sb.AppendLine($"{{\"role\":\"user\",\"content\":[{{\"type\":\"tool_result\",\"tool_use_id\":\"{i}\",\"content\":\"ok\"}}]}}");
        }
        // Add 3 identical Edit calls to trigger retry_loop
        for (int i = 0; i < 3; i++)
        {
            sb.AppendLine("{\"role\":\"assistant\",\"content\":[{\"type\":\"tool_use\",\"name\":\"Edit\",\"input\":{\"file_path\":\"src/Window.cs\",\"action\":\"edit\",\"content\":\"fix\"}}]}");
            sb.AppendLine($"{{\"role\":\"user\",\"content\":[{{\"type\":\"tool_result\",\"tool_use_id\":\"e{i}\",\"content\":\"ok\"}}]}}");
        }

        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(sb.ToString());

        signals.Should().NotBeEmpty();
        MistakeSignal? retrySignal = signals.FirstOrDefault(s => s.SignalType == "retry_loop");
        retrySignal.Should().NotBeNull();
        // Context should be windowed — max ~30 lines (20 before + 10 after)
        int contextLineCount = retrySignal!.Context.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        contextLineCount.Should().BeLessOrEqualTo(35); // small tolerance
    }

    // -------------------------------------------------------------------------
    // Test 6: Empty JSONL returns empty list
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DetectAsync_EmptyJsonl_ReturnsEmptyList()
    {
        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(string.Empty);

        signals.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Test 7: 2 identical tool calls (below threshold) returns no retry_loop
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DetectAsync_TwoIdenticalCalls_NoRetryLoopSignal()
    {
        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(TwoIdenticalCallsJsonl);

        signals.Should().NotContain(s => s.SignalType == "retry_loop");
    }
}
