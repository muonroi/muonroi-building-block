using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime.Internal;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.File;

/// <summary>
/// <see cref="IExperienceStore"/> implementation backed by JSON files on disk.
/// One file per tier: principles.json, behavioral.json, selfqa.json, raw.json.
/// Concurrent writes to the same tier are serialised via per-tier <see cref="SemaphoreSlim"/> locks.
/// </summary>
public sealed class FileExperienceStore : IExperienceStore
{
    private readonly string _directoryPath;
    private readonly ExperienceBudgetConfig _budget;
    private readonly IExperienceBrain? _brain;
    private readonly IMLog<FileExperienceStore>? _log;
    private readonly ConcurrentDictionary<ExperienceTier, SemaphoreSlim> _semaphores;

    /// <summary>
    /// Initialises a new <see cref="FileExperienceStore"/>.
    /// Creates the backing directory if it does not already exist.
    /// </summary>
    public FileExperienceStore(IOptions<ExperienceStoreOptions> options, IMLog<FileExperienceStore>? log = null, IExperienceBrain? brain = null)
    {
        _directoryPath = options.Value.FileDirectoryPath;
        _budget = options.Value.Budget;
        _brain = brain;
        _log = log;

        Directory.CreateDirectory(_directoryPath);

        _semaphores = new ConcurrentDictionary<ExperienceTier, SemaphoreSlim>();
        foreach (ExperienceTier tier in Enum.GetValues<ExperienceTier>())
        {
            _semaphores[tier] = new SemaphoreSlim(1, 1);
        }
    }

    // -------------------------------------------------------------------------
    // IExperienceStore
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<bool> StoreAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        SemaphoreSlim sem = _semaphores[experience.Tier];
        await sem.WaitAsync(ct);
        try
        {
            List<NeuronExperience> existing = await LoadTierAsync(experience.Tier, ct);

            if (experience.Tier != ExperienceTier.RawTrajectory)
            {
                int budget = TokenBudgetEnforcer.GetBudgetForTier(experience.Tier, _budget);
                if (TokenBudgetEnforcer.WouldExceedBudget(existing, experience, budget))
                {
                    _log?.LogWarning(
                        "Budget exceeded for tier {Tier}, rejecting entry {Id}",
                        experience.Tier, experience.Id);
                    return false;
                }
            }

            existing.Add(experience);
            await SaveTierAsync(experience.Tier, existing, ct);
            return true;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ExperienceSearchResult>> FindRelevantAsync(
        string query,
        int topK = 5,
        CancellationToken ct = default)
    {
        // Load all searchable tiers (skip RawTrajectory — too noisy).
        var searchableTiers = new[] { ExperienceTier.Principle, ExperienceTier.Behavioral, ExperienceTier.SelfQA };

        var allEntries = new List<NeuronExperience>();
        foreach (ExperienceTier tier in searchableTiers)
        {
            allEntries.AddRange(await LoadTierAsync(tier, ct));
        }

        // Simple keyword scoring: count query-word hits across text fields.
        string[] queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryWords.Length == 0)
        {
            return Enumerable.Empty<ExperienceSearchResult>();
        }

        var scored = allEntries
            .Select(e =>
            {
                string corpus = $"{e.Trigger} {e.Question} {e.Solution}".ToLowerInvariant();
                int hits = queryWords.Count(w => corpus.Contains(w.ToLowerInvariant()));
                return (Entry: e, Score: hits > 0 ? (float)hits / queryWords.Length : 0f);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new ExperienceSearchResult(x.Entry, x.Score));

        return scored;
    }

    /// <inheritdoc />
    public async Task<NeuronExperience> PromoteAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        if (experience.Tier == ExperienceTier.Principle)
        {
            throw new InvalidOperationException("Cannot promote a Tier 0 (Principle) entry — it is already the highest tier.");
        }

        var targetTier = (ExperienceTier)((int)experience.Tier - 1);
        NeuronExperience promoted = experience with { Tier = targetTier };

        // Remove from source tier (serialised).
        SemaphoreSlim srcSem = _semaphores[experience.Tier];
        await srcSem.WaitAsync(ct);
        try
        {
            List<NeuronExperience> src = await LoadTierAsync(experience.Tier, ct);
            src.RemoveAll(e => e.Id == experience.Id);
            await SaveTierAsync(experience.Tier, src, ct);
        }
        finally
        {
            srcSem.Release();
        }

        // Add to destination tier (serialised).
        SemaphoreSlim dstSem = _semaphores[targetTier];
        await dstSem.WaitAsync(ct);
        try
        {
            List<NeuronExperience> dst = await LoadTierAsync(targetTier, ct);
            dst.Add(promoted);
            await SaveTierAsync(targetTier, dst, ct);
        }
        finally
        {
            dstSem.Release();
        }

        return promoted;
    }

    /// <inheritdoc />
    public async Task<NeuronExperience> DemoteAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        if (experience.Tier == ExperienceTier.RawTrajectory)
        {
            throw new InvalidOperationException("Cannot demote a Tier 3 (RawTrajectory) entry — it is already the lowest tier.");
        }

        var targetTier = (ExperienceTier)((int)experience.Tier + 1);
        NeuronExperience demoted = experience with { Tier = targetTier, HitCount = 0 };

        SemaphoreSlim srcSem = _semaphores[experience.Tier];
        await srcSem.WaitAsync(ct);
        try
        {
            List<NeuronExperience> src = await LoadTierAsync(experience.Tier, ct);
            src.RemoveAll(e => e.Id == experience.Id);
            await SaveTierAsync(experience.Tier, src, ct);
        }
        finally
        {
            srcSem.Release();
        }

        SemaphoreSlim dstSem = _semaphores[targetTier];
        await dstSem.WaitAsync(ct);
        try
        {
            List<NeuronExperience> dst = await LoadTierAsync(targetTier, ct);
            dst.Add(demoted);
            await SaveTierAsync(targetTier, dst, ct);
        }
        finally
        {
            dstSem.Release();
        }

        return demoted;
    }

    /// <inheritdoc />
    /// <remarks>ClusterAndAbstractAsync requires an IExperienceBrain — register via AddExperienceBrain() before use.</remarks>
    public async Task<NeuronExperience> ClusterAndAbstractAsync(IEnumerable<NeuronExperience> tier2Entries, CancellationToken ct = default)
    {
        if (_brain is null)
        {
            throw new NotSupportedException("IExperienceBrain not registered — call AddExperienceBrain() before using ClusterAndAbstractAsync");
        }

        NeuronExperience[] cluster = tier2Entries.ToArray();
        string prompt = BuildAbstractionPrompt(cluster);

        NeuronExperience principle = await _brain.AbstractAsync(prompt, ct);

        NeuronExperience stored = principle with
        {
            Id = Guid.NewGuid().ToString(),
            Tier = ExperienceTier.Principle,
            Principle = principle.Solution,
            CreatedFrom = "evolution-engine",
            CreatedAt = DateTimeOffset.UtcNow
        };

        bool success = await StoreAsync(stored, ct);
        if (!success)
        {
            throw new InvalidOperationException("Principle budget exceeded — source entries preserved");
        }

        return stored;
    }

    private static string BuildAbstractionPrompt(NeuronExperience[] cluster)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Given these {cluster.Length} related experiences:");
        foreach (NeuronExperience e in cluster)
            sb.AppendLine($"- Trigger: {e.Trigger}, Solution: {e.Solution}");
        sb.AppendLine();
        sb.AppendLine("Extract ONE general principle that covers all cases.");
        sb.AppendLine("Format: \"When [general condition], always [general action] because [general reason]\"");
        sb.AppendLine("Respond ONLY in JSON: {\"trigger\":\"\",\"question\":\"\",\"reasoning\":[],\"solution\":\"\"}");
        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NeuronExperience>> FindAllInTierAsync(ExperienceTier tier, CancellationToken ct = default)
    {
        SemaphoreSlim sem = _semaphores[tier];
        await sem.WaitAsync(ct);
        try
        {
            return await LoadTierAsync(tier, ct);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        // Scan all tiers, short-circuit after first successful removal.
        foreach (ExperienceTier tier in Enum.GetValues<ExperienceTier>())
        {
            SemaphoreSlim sem = _semaphores[tier];
            await sem.WaitAsync(ct);
            try
            {
                List<NeuronExperience> entries = await LoadTierAsync(tier, ct);
                int removed = entries.RemoveAll(e => e.Id == id);
                if (removed > 0)
                {
                    await SaveTierAsync(tier, entries, ct);
                    _log?.Debug("Deleted entry {Id} from tier {Tier}", id, tier);
                    return;
                }
            }
            finally
            {
                sem.Release();
            }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string GetFilePath(ExperienceTier tier) => Path.Combine(
        _directoryPath,
        tier switch
        {
            ExperienceTier.Principle => "principles.json",
            ExperienceTier.Behavioral => "behavioral.json",
            ExperienceTier.SelfQA => "selfqa.json",
            ExperienceTier.RawTrajectory => "raw.json",
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, $"Unknown ExperienceTier: {tier}")
        });

    private async Task<List<NeuronExperience>> LoadTierAsync(ExperienceTier tier, CancellationToken ct)
    {
        string path = GetFilePath(tier);
        if (!System.IO.File.Exists(path))
        {
            return new List<NeuronExperience>();
        }

        await using FileStream stream = System.IO.File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<NeuronExperience>>(stream, cancellationToken: ct)
               ?? new List<NeuronExperience>();
    }

    private async Task SaveTierAsync(ExperienceTier tier, List<NeuronExperience> entries, CancellationToken ct)
    {
        string path = GetFilePath(tier);
        await using FileStream stream = System.IO.File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, entries, new JsonSerializerOptions { WriteIndented = true }, ct);
    }
}
