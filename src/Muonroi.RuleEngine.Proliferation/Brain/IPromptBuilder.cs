using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Builds system and user prompts for rule proliferation analysis.
/// Shared across all brain providers (Ollama, OpenAI, Claude).
/// </summary>
public interface IPromptBuilder
{
    string BuildSystemPrompt();

    string BuildUserPrompt(
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        int budget,
        IReadOnlyList<string>? focusAreas);
}
