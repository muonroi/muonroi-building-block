using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Muonroi.Experience.Abstractions;

namespace Muonroi.Experience.Runtime.Interception;

/// <summary>
/// Default implementation of IExperienceInterceptor.
/// Queries the store for relevant experiences and returns a formatted context string.
/// Returns empty string when no relevant experience is found or the token is cancelled.
/// Confidence routing:
///   >= 0.8 — full experience block "[Experience] trigger: solution"
///   0.5-0.8 — condensed hint "[Hint] trigger"
///   &lt; 0.5  — empty string (low relevance, no injection)
/// </summary>
public sealed class DefaultExperienceInterceptor : IExperienceInterceptor
{
    private readonly IExperienceStore _store;

    /// <summary>Initializes a new instance with the given experience store.</summary>
    public DefaultExperienceInterceptor(IExperienceStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public async Task<string> InterceptAsync(
        string toolName,
        string toolContext,
        CancellationToken ct = default)
    {
        try
        {
            var results = (await _store.FindRelevantAsync($"{toolName} {toolContext}", topK: 3, ct)).ToList();
            if (results.Count == 0) return string.Empty;

            var top = results[0];
            if (top.RelevanceScore < 0.5f) return string.Empty;

            // High confidence (>= 0.8): full experience block
            if (top.RelevanceScore >= 0.8f)
                return $"[Experience] {top.Experience.Trigger}: {top.Experience.Solution}";

            // Medium confidence (0.5-0.8): condensed hint
            return $"[Hint] {top.Experience.Trigger}";
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }
}
