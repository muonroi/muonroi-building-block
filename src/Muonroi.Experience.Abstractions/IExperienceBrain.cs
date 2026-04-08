using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
}
