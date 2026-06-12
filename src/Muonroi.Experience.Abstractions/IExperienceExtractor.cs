using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.Experience.Abstractions;

/// <summary>
/// Detects mistake signals in session logs (retry loops, user corrections, test failures)
/// and extracts structured Self-QA NeuronExperience entries (Question → Why → Solution).
/// </summary>
public interface IExperienceExtractor
{
    /// <summary>
    /// Scans the session log for mistake signals and extracts structured QA experience entries.
    /// Each returned entry has Tier set to ExperienceTier.SelfQA and Confidence in the 0.4–0.6 range.
    /// </summary>
    /// <param name="sessionLog">Raw session trajectory text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted Self-QA entries. Empty if no mistakes detected.</returns>
    Task<IEnumerable<NeuronExperience>> ExtractQAAsync(
        string sessionLog,
        CancellationToken ct = default);
}
