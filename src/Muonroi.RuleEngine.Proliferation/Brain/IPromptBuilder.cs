using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Builds system and user prompts for rule proliferation analysis.
/// Shared across all brain providers (Ollama, OpenAI, Claude).
/// </summary>
public interface IPromptBuilder
{
    string BuildSystemPrompt();

    /// <summary>
    /// Builds a system prompt tailored to the specific rule type.
    /// Falls back to generic prompt for Unknown kind.
    /// </summary>
    string BuildSystemPrompt(RuleSetKind kind);

    string BuildUserPrompt(
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        int budget,
        IReadOnlyList<string>? focusAreas);
}
