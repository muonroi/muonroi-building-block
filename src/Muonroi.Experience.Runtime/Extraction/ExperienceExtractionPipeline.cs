using Microsoft.Extensions.Options;
using Muonroi.Experience.Abstractions;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.Extraction;

/// <summary>
/// Orchestrates the full experience extraction pipeline: detect mistakes -> extract via brain
/// -> dedup against store -> assign confidence -> store.
/// Implements <see cref="IExperienceExtractor"/> for plug-in by external hooks.
/// </summary>
public sealed class ExperienceExtractionPipeline : IExperienceExtractor
{
    private readonly MistakeDetector _detector;
    private readonly IExperienceBrain _brain;
    private readonly IExperienceStore _store;
    private readonly ExperienceBudgetConfig _budget;
    private readonly IMLog<ExperienceExtractionPipeline>? _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ExperienceExtractionPipeline"/>.
    /// </summary>
    /// <param name="detector">Detects mistake signals in raw JSONL session logs.</param>
    /// <param name="brain">LLM-powered extractor that converts signal context into NeuronExperience entries.</param>
    /// <param name="store">Backing store used for dedup checks (FindRelevantAsync) and persistence (StoreAsync).</param>
    /// <param name="storeOptions">Store options containing the ExperienceBudgetConfig (DedupThreshold, confidence clamp).</param>
    /// <param name="logger">Optional structured logger.</param>
    public ExperienceExtractionPipeline(
        MistakeDetector detector,
        IExperienceBrain brain,
        IExperienceStore store,
        IOptions<ExperienceStoreOptions> storeOptions,
        IMLog<ExperienceExtractionPipeline>? logger = null)
    {
        _detector = detector;
        _brain = brain;
        _store = store;
        _budget = storeOptions.Value.Budget;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Pipeline steps per signal:
    /// 1. DetectAsync: parse JSONL, emit MistakeSignal list.
    /// 2. ExtractAsync: LLM converts each signal's context into NeuronExperience entries.
    /// 3. ComputeConfidence: assign signal-type-specific confidence, clamped to [InitialConfidenceMin, InitialConfidenceMax].
    /// 4. CheckNearDuplicateAsync: cosine similarity check against store (topK=1). Threshold = DedupThreshold (0.85).
    ///    - Duplicate: increment hitCount on existing entry, store updated entry, skip new entry.
    ///    - Not duplicate: store new entry, add to result list.
    /// </remarks>
    public async Task<IEnumerable<NeuronExperience>> ExtractQAAsync(string sessionLog, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionLog))
            return [];

        IReadOnlyList<MistakeSignal> signals = await _detector.DetectAsync(sessionLog, ct);
        if (signals.Count == 0)
            return [];

        var extracted = new List<NeuronExperience>();

        foreach (MistakeSignal signal in signals)
        {
            IEnumerable<NeuronExperience> entries = await _brain.ExtractAsync(signal.Context, ct);

            foreach (NeuronExperience entry in entries)
            {
                float confidence = ComputeConfidence(signal.SignalType);
                confidence = Math.Clamp(confidence, _budget.InitialConfidenceMin, _budget.InitialConfidenceMax);

                NeuronExperience scored = entry with
                {
                    Confidence = confidence,
                    Tier = ExperienceTier.SelfQA
                };

                (bool isDuplicate, NeuronExperience? existingEntry) = await CheckNearDuplicateAsync(scored, ct);

                if (isDuplicate && existingEntry is not null)
                {
                    // Increment hitCount on the existing entry and persist — do NOT add to result
                    NeuronExperience updated = existingEntry with { HitCount = existingEntry.HitCount + 1 };
                    await _store.StoreAsync(updated, ct);
                }
                else
                {
                    await _store.StoreAsync(scored, ct);
                    extracted.Add(scored);
                }
            }
        }

        return extracted;
    }

    /// <summary>
    /// Maps a signal type to its initial confidence score per EXT-04 specification.
    /// </summary>
    private static float ComputeConfidence(string signalType) => signalType switch
    {
        "retry_loop"      => 0.6f,
        "user_correction" => 0.6f,
        "test_red_green"  => 0.6f,
        "git_revert"      => 0.4f,
        _                 => 0.5f
    };

    /// <summary>
    /// Checks whether a near-duplicate of <paramref name="entry"/> already exists in the store.
    /// Uses FindRelevantAsync (topK=1) and compares RelevanceScore against DedupThreshold (default 0.85).
    /// </summary>
    /// <returns>
    /// A tuple: (true, existingEntry) if a duplicate was found; (false, null) otherwise.
    /// </returns>
    private async Task<(bool isDuplicate, NeuronExperience? existingEntry)> CheckNearDuplicateAsync(
        NeuronExperience entry, CancellationToken ct)
    {
        string query = $"{entry.Trigger} {entry.Question}";
        IEnumerable<ExperienceSearchResult> results = await _store.FindRelevantAsync(query, topK: 1, ct);

        ExperienceSearchResult? match = results.FirstOrDefault(r => r.RelevanceScore >= _budget.DedupThreshold);
        if (match is null)
            return (false, null);

        _logger?.Info(
            "Near-duplicate detected (score={Score:F2}), incrementing hitCount on existing entry Id={Id}",
            match.RelevanceScore,
            match.Experience.Id);

        return (true, match.Experience);
    }
}
