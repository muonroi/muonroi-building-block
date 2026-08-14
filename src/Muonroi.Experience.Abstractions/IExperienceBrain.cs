namespace Muonroi.Experience.Abstractions;

/// <summary>
/// Extracts NeuronExperience entries from raw session data.
/// Implement this interface to plug in an LLM-powered or rule-based extraction strategy.
/// Mirrors IRuleProliferationBrain — see CompositeExperienceBrain for fallback chaining.
/// </summary>
public interface IExperienceBrain
{
    /// <summary>
    /// Analyzes a session log and extracts zero or more experience entries.
    /// </summary>
    /// <param name="sessionLog">Raw session trajectory text (tool calls, corrections, retries).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted experience entries. May be empty if no mistakes are detected.</returns>
    Task<IEnumerable<NeuronExperience>> ExtractAsync(
        string sessionLog,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a single generalized principle from a cluster of related experiences.
    /// Uses a different system prompt than <see cref="ExtractAsync"/> — abstraction, not extraction.
    /// </summary>
    /// <param name="abstractionPrompt">
    /// Pre-formatted prompt containing all cluster entries and the abstraction instruction.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A single <see cref="NeuronExperience"/> representing the abstracted principle.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the brain returns no result.</exception>
    Task<NeuronExperience> AbstractAsync(
        string abstractionPrompt,
        CancellationToken ct = default);
}
