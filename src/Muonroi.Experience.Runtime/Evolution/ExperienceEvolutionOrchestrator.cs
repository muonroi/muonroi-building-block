namespace Muonroi.Experience.Runtime.Evolution;

/// <summary>
/// Sequences the full evolution lifecycle:
/// (1) promotion sweep — Tier 2 entries with HitCount ≥ threshold promoted to Tier 1,
/// (2) cluster-and-abstract — qualifying Tier 2 clusters abstracted to Tier 0 principles,
/// (3) archival sweep — stale Tier 2 entries (HitCount=0, age &gt; threshold) moved to archive.
///
/// Emits an <see cref="EvolutionReport"/> with before/after token counts and compression ratio.
/// The compression ratio is the primary observable invariant: values below 1.0 mean memory SHRANK.
/// </summary>
/// <remarks>
/// Initialises a new <see cref="ExperienceEvolutionOrchestrator"/>.
/// </remarks>
public sealed class ExperienceEvolutionOrchestrator(
    IExperienceStore store,
    IExperienceBrain brain,
    IExperienceArchive archive,
    EvolutionOptions opts,
    ExperienceBudgetConfig budget,
    IMLog<ExperienceEvolutionOrchestrator>? log = null)
{
    private readonly IExperienceStore _store = MGuard.NotNull(store);
    private readonly IExperienceBrain _brain = MGuard.NotNull(brain);
    private readonly IExperienceArchive _archive = MGuard.NotNull(archive);
    private readonly EvolutionOptions _opts = MGuard.NotNull(opts);
    private readonly ExperienceBudgetConfig _budget = MGuard.NotNull(budget);
    private readonly IMLog<ExperienceEvolutionOrchestrator>? _log = log;

    /// <summary>
    /// Runs the full evolution cycle and returns an <see cref="EvolutionReport"/>.
    /// </summary>
    public async Task<EvolutionReport> RunEvolutionAsync(CancellationToken ct = default)
    {
        var report = new EvolutionReport { RunAt = DateTimeOffset.UtcNow };

        // Snapshot token count before
        IEnumerable<NeuronExperience> tier2Before = await _store.FindAllInTierAsync(ExperienceTier.SelfQA, ct);
        report.TokensBefore = tier2Before.Sum(TokenBudgetEnforcer.EstimateTokens);

        // Step 1: Promotion sweep
        await RunPromotionSweepAsync(report, ct);

        // Step 2: Cluster and abstract
        await RunClusterAbstractAsync(report, ct);

        // Step 3: Archival sweep
        await RunArchivalSweepAsync(report, ct);

        // Snapshot token count after
        IEnumerable<NeuronExperience> tier2After = await _store.FindAllInTierAsync(ExperienceTier.SelfQA, ct);
        report.TokensAfter = tier2After.Sum(TokenBudgetEnforcer.EstimateTokens);

        report.CompressionRatio = report.TokensBefore > 0
            ? (float)report.TokensAfter / report.TokensBefore
            : 1.0f;

        _log?.Info("Evolution run complete:\n{Report}", report.ToString());

        return report;
    }

    // -------------------------------------------------------------------------
    // Private steps
    // -------------------------------------------------------------------------

    private async Task RunPromotionSweepAsync(EvolutionReport report, CancellationToken ct)
    {
        IEnumerable<NeuronExperience> tier2 = await _store.FindAllInTierAsync(ExperienceTier.SelfQA, ct);

        foreach (NeuronExperience entry in tier2)
        {
            if (entry.HitCount < _budget.PromotionHitThreshold) continue;

            // Format as behavioral rule — strip reasoning, keep concise
            NeuronExperience behavioral = entry with
            {
                Solution = $"WHEN {entry.Trigger} → {entry.Solution}",
                Reasoning = []
            };

            try
            {
                await _store.PromoteAsync(behavioral, ct);
                report.PromotedCount++;
                _log?.Debug("Promoted entry {Id} to Behavioral tier", entry.Id);
            }
            catch (Exception ex)
            {
                _log?.Warn("Failed to promote entry {Id} — {Error}", entry.Id, ex.Message);
            }
        }
    }

    private async Task RunClusterAbstractAsync(EvolutionReport report, CancellationToken ct)
    {
        IEnumerable<NeuronExperience> tier2 = await _store.FindAllInTierAsync(ExperienceTier.SelfQA, ct);
        IReadOnlyList<NeuronExperience> entries = tier2.ToList().AsReadOnly();

        if (entries.Count == 0) return;

        List<List<NeuronExperience>> clusters = ClusteringEngine.GroupBySimilarity(entries, _opts.ClusterSimilarityThreshold);
        report.ClustersFormed = clusters.Count(c => c.Count >= _opts.MinClusterSize);

        foreach (List<NeuronExperience> cluster in clusters)
        {
            if (cluster.Count < _opts.MinClusterSize) continue;

            try
            {
                NeuronExperience principle = await _store.ClusterAndAbstractAsync(cluster, ct);

                // Delete source entries from Tier 2 only after successful principle storage
                foreach (NeuronExperience source in cluster)
                {
                    try
                    {
                        await _store.DeleteAsync(source.Id, ct);
                    }
                    catch (Exception ex)
                    {
                        _log?.Warn("Failed to delete source entry {Id} after abstraction — {Error}", source.Id, ex.Message);
                    }
                }

                report.PrinciplesCreated++;
                _log?.Debug("Created principle {PrincipleId} from cluster of {Count} entries", principle.Id, cluster.Count);
            }
            catch (InvalidOperationException ex)
            {
                // Budget exceeded or brain failure — do NOT delete source entries (Pitfall 3)
                _log?.Warn("Cluster abstraction failed — source entries preserved — {Error}", ex.Message);
            }
            catch (NotSupportedException ex)
            {
                // Brain not registered — skip cluster-abstract step entirely
                _log?.Warn("Cluster abstraction skipped — brain not available — {Error}", ex.Message);
            }
        }
    }

    private async Task RunArchivalSweepAsync(EvolutionReport report, CancellationToken ct)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-_budget.ArchivalDaysThreshold);
        IEnumerable<NeuronExperience> tier2 = await _store.FindAllInTierAsync(ExperienceTier.SelfQA, ct);

        foreach (NeuronExperience entry in tier2)
        {
            if (entry.HitCount > 0 || entry.CreatedAt > cutoff) continue;

            try
            {
                await _archive.ArchiveAsync(entry, ct);
                await _store.DeleteAsync(entry.Id, ct);
                report.ArchivedCount++;
                _log?.Debug("Archived stale entry {Id} (HitCount=0, age={Age}d)", entry.Id,
                    (DateTimeOffset.UtcNow - entry.CreatedAt).TotalDays);
            }
            catch (Exception ex)
            {
                _log?.Warn("Failed to archive entry {Id} — {Error}", entry.Id, ex.Message);
            }
        }
    }
}
