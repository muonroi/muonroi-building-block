using System.Text.Json;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.Extraction;

/// <summary>
/// Parses Claude Code JSONL session logs and detects mistake patterns using 4 heuristics:
/// retry_loop, user_correction, git_revert, test_red_green.
/// </summary>
public sealed class MistakeDetector(IMLog<MistakeDetector>? logger = null)
{
    private const int ContextLinesBefore = 20;
    private const int ContextLinesAfter = 10;
    private const int RetryLoopThreshold = 3;

    private static readonly JsonDocumentOptions JsonDocOpts = new() { AllowTrailingCommas = true };

    /// <summary>
    /// Parses the raw JSONL session log and returns all detected mistake signals.
    /// </summary>
    public Task<IReadOnlyList<MistakeSignal>> DetectAsync(string rawJsonl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawJsonl))
            return Task.FromResult<IReadOnlyList<MistakeSignal>>([]);

        string[] lines = rawJsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return Task.FromResult<IReadOnlyList<MistakeSignal>>([]);

        var signals = new List<MistakeSignal>();

        // State for tracking across lines
        // key = "tool_name|file_path|action" -> list of line indices where this key appeared
        var toolCallOccurrences = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        // For user_correction: track whether previous assistant line had tool_use
        bool prevAssistantHadToolUse = false;
        int prevAssistantLineIndex = -1;

        // For test_red_green: track state machine
        bool sawTestFailure = false;
        int testFailLineIndex = -1;
        bool sawEditAfterFailure = false;

        for (int i = 0; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(line, JsonDocOpts);
            }
            catch (JsonException)
            {
                logger?.Warn("MistakeDetector: skipping malformed JSONL line {Index}", i);
                continue;
            }

            using (doc)
            {
                JsonElement root = doc.RootElement;
                string? role = root.TryGetProperty("role", out JsonElement roleEl) ? roleEl.GetString() : null;

                if (!root.TryGetProperty("content", out JsonElement contentEl))
                    continue;

                bool lineHasToolUse = false;
                bool lineHasToolResult = false;

                // Iterate content array
                if (contentEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in contentEl.EnumerateArray())
                    {
                        if (!item.TryGetProperty("type", out JsonElement typeEl)) continue;
                        string? contentType = typeEl.GetString();

                        if (contentType == "tool_use")
                        {
                            lineHasToolUse = true;
                            string toolName = item.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() ?? "" : "";
                            string filePath = "";
                            string action = "";

                            if (item.TryGetProperty("input", out JsonElement inputEl))
                            {
                                filePath = inputEl.TryGetProperty("file_path", out JsonElement fpEl) ? fpEl.GetString() ?? "" : "";
                                action = inputEl.TryGetProperty("action", out JsonElement actEl) ? actEl.GetString() ?? "" : "";

                                // Git revert detection: Bash tool with git revert/reset
                                if (toolName == "Bash" && inputEl.TryGetProperty("command", out JsonElement cmdEl))
                                {
                                    string? cmd = cmdEl.GetString();
                                    if (cmd is not null && IsGitRevertCommand(cmd))
                                    {
                                        string context = BuildContext(lines, i, ContextLinesBefore, ContextLinesAfter);
                                        signals.Add(new MistakeSignal(
                                            "git_revert",
                                            context,
                                            [$"Bash|{cmd}|git_revert"],
                                            DateTimeOffset.UtcNow));
                                    }
                                }
                            }

                            // Build composite key for retry_loop detection
                            string compositeKey = $"{toolName}|{filePath}|{action}";
                            if (!toolCallOccurrences.TryGetValue(compositeKey, out List<int>? occurrences))
                            {
                                occurrences = [];
                                toolCallOccurrences[compositeKey] = occurrences;
                            }
                            occurrences.Add(i);

                            // Emit retry_loop when threshold reached (emit exactly once per key per threshold crossing)
                            if (occurrences.Count == RetryLoopThreshold)
                            {
                                int firstLine = occurrences[0];
                                int lastLine = occurrences[^1];
                                string context = BuildContext(lines, firstLine, ContextLinesBefore, ContextLinesAfter + (lastLine - firstLine));
                                signals.Add(new MistakeSignal(
                                    "retry_loop",
                                    context,
                                    occurrences.Select(_ => compositeKey).ToArray(),
                                    DateTimeOffset.UtcNow));
                            }

                            // Track for test_red_green: Edit/Write after failure
                            if (sawTestFailure && (toolName == "Edit" || toolName == "Write"))
                            {
                                sawEditAfterFailure = true;
                            }
                        }
                        else if (contentType == "tool_result")
                        {
                            lineHasToolResult = true;
                            string? resultContent = null;
                            if (item.TryGetProperty("content", out JsonElement resultEl))
                            {
                                resultContent = resultEl.ValueKind == JsonValueKind.String
                                    ? resultEl.GetString()
                                    : resultEl.ToString();
                            }

                            // test_red_green: detect failure then pass sequence
                            if (resultContent is not null)
                            {
                                if (IsTestFailure(resultContent))
                                {
                                    sawTestFailure = true;
                                    sawEditAfterFailure = false;
                                    testFailLineIndex = i;
                                }
                                else if (sawTestFailure && sawEditAfterFailure && IsTestPass(resultContent))
                                {
                                    string context = BuildContext(lines, testFailLineIndex, ContextLinesBefore, i - testFailLineIndex + ContextLinesAfter);
                                    signals.Add(new MistakeSignal(
                                        "test_red_green",
                                        context,
                                        ["Bash|test_failure|test_pass"],
                                        DateTimeOffset.UtcNow));
                                    // Reset state
                                    sawTestFailure = false;
                                    sawEditAfterFailure = false;
                                    testFailLineIndex = -1;
                                }
                            }
                        }
                    }
                }

                // user_correction detection: user message with non-tool_result content after assistant tool_use
                if (role == "user" && !lineHasToolResult && prevAssistantHadToolUse)
                {
                    // Check if user message has actual text content
                    bool hasText = false;
                    if (contentEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in contentEl.EnumerateArray())
                        {
                            if (item.TryGetProperty("type", out JsonElement t) && t.GetString() == "text")
                            {
                                hasText = true;
                                break;
                            }
                        }
                    }
                    else if (contentEl.ValueKind == JsonValueKind.String)
                    {
                        hasText = !string.IsNullOrWhiteSpace(contentEl.GetString());
                    }

                    if (hasText)
                    {
                        string context = BuildContext(lines, prevAssistantLineIndex, ContextLinesBefore, ContextLinesAfter);
                        signals.Add(new MistakeSignal(
                            "user_correction",
                            context,
                            ["user|correction|text"],
                            DateTimeOffset.UtcNow));
                    }
                }

                // Update tracking state for next iteration
                if (role == "assistant")
                {
                    prevAssistantHadToolUse = lineHasToolUse;
                    prevAssistantLineIndex = i;
                }
                else if (role == "user")
                {
                    // Reset after user line
                    prevAssistantHadToolUse = false;
                }
            }
        }

        return Task.FromResult<IReadOnlyList<MistakeSignal>>(signals);
    }

    private static bool IsGitRevertCommand(string command)
    {
        string lower = command.ToLowerInvariant();
        return lower.Contains("git revert") || lower.Contains("git reset");
    }

    private static bool IsTestFailure(string content)
    {
        return content.Contains("FAILED", StringComparison.OrdinalIgnoreCase)
            || content.Contains("failed", StringComparison.Ordinal)
            || content.Contains("Error", StringComparison.Ordinal);
    }

    private static bool IsTestPass(string content)
    {
        return content.Contains("passed", StringComparison.OrdinalIgnoreCase)
            || content.Contains("PASSED", StringComparison.Ordinal)
            || content.Contains("OK", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds a windowed context string: [max(0, signalLine-before) .. min(totalLines, signalLine+after)].
    /// </summary>
    private static string BuildContext(string[] lines, int signalLine, int linesBefore, int linesAfter)
    {
        int start = Math.Max(0, signalLine - linesBefore);
        int end = Math.Min(lines.Length - 1, signalLine + linesAfter);
        return string.Join('\n', lines[start..(end + 1)]);
    }
}
