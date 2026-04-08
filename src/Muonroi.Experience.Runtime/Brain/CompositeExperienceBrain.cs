using Muonroi.Experience.Abstractions;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.Brain;

/// <summary>
/// Composite IExperienceBrain with fallback-only semantics (per EXT-05).
/// Calls primary first; on empty result or exception, calls fallback.
/// If both fail, returns empty without propagating exceptions.
/// </summary>
public sealed class CompositeExperienceBrain(
    IExperienceBrain primary,
    IExperienceBrain fallback,
    IMLog<CompositeExperienceBrain>? logger = null) : IExperienceBrain
{
    /// <inheritdoc/>
    public async Task<IEnumerable<NeuronExperience>> ExtractAsync(string sessionLog, CancellationToken ct = default)
    {
        try
        {
            IEnumerable<NeuronExperience> result = await primary.ExtractAsync(sessionLog, ct);
            if (result.Any()) return result;
            logger?.Warn("Primary brain returned empty, trying fallback");
        }
        catch (Exception ex)
        {
            logger?.Warn("Primary brain failed, trying fallback — {Error}", ex.Message);
        }

        try
        {
            return await fallback.ExtractAsync(sessionLog, ct);
        }
        catch (Exception ex)
        {
            logger?.Warn("Fallback brain also failed, returning empty — {Error}", ex.Message);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<NeuronExperience> AbstractAsync(string abstractionPrompt, CancellationToken ct = default)
    {
        try
        {
            return await primary.AbstractAsync(abstractionPrompt, ct);
        }
        catch (Exception ex)
        {
            logger?.Warn("Primary brain AbstractAsync failed, trying fallback — {Error}", ex.Message);
        }

        return await fallback.AbstractAsync(abstractionPrompt, ct);
    }
}
