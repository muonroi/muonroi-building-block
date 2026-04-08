namespace Muonroi.Experience.Runtime.Extraction;

/// <summary>
/// Represents a detected mistake or correction pattern in a Claude Code JSONL session log.
/// </summary>
/// <param name="SignalType">Type of signal: "retry_loop" | "user_correction" | "git_revert" | "test_red_green"</param>
/// <param name="Context">Windowed session excerpt (~20 lines before + 10 after the signal line)</param>
/// <param name="ToolCalls">Tool call keys involved, formatted as "tool_name|file_path|action"</param>
/// <param name="DetectedAt">UTC timestamp when this signal was detected</param>
public record MistakeSignal(
    string SignalType,
    string Context,
    string[] ToolCalls,
    DateTimeOffset DetectedAt
);
