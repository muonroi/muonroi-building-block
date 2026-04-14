using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.Experience.Abstractions;

/// <summary>
/// Injects relevant NeuronExperience entries into the agent context before a tool action executes.
/// Called by the PreToolUse hook. Must complete within a timeout (see ExperienceBudgetConfig) to avoid blocking.
/// </summary>
public interface IExperienceInterceptor
{
    /// <summary>
    /// Queries the store for experiences relevant to the pending tool action and returns
    /// an annotated context string to prepend to the agent prompt.
    /// Returns null or empty string if no relevant experience found or timeout exceeded.
    /// </summary>
    /// <param name="toolName">Name of the tool about to be invoked (e.g., "Edit", "Write", "Bash").</param>
    /// <param name="toolContext">Contextual description of what the tool will do.</param>
    /// <param name="ct">Cancellation token — caller enforces timeout by cancelling this token.</param>
    /// <returns>Annotated context to inject, or empty string if nothing to inject.</returns>
    Task<string> InterceptAsync(
        string toolName,
        string toolContext,
        CancellationToken ct = default);
}
