using System.Collections.Concurrent;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Two-pass deduplicator: hash-based first (fast exact), then semantic embedding (fuzzy).
/// Falls back to hash-only if embedder returns empty vectors.
/// </summary>
public sealed class VectorSemanticDeduplicator(
    IVectorEmbedder embedder,
    ProliferationOptions options,
    InputHashDeduplicator hashDedup,
    IMLog<VectorSemanticDeduplicator>? logger = null) : IScenarioDeduplicator
{
    private readonly ConcurrentDictionary<string, float[]> _embeddingCache = new();

    /// <summary>Removes duplicate scenarios using hash and semantic similarity checks.</summary>
    public IReadOnlyList<NeuronScenario> Deduplicate(
        IReadOnlyList<NeuronScenario> candidates,
        IReadOnlyList<NeuronScenario> existingScenarios)
    {
        // Pass 1: exact hash dedup (fast)
        IReadOnlyList<NeuronScenario> hashFiltered = hashDedup.Deduplicate(candidates, existingScenarios);

        if (hashFiltered.Count == 0)
            return hashFiltered;

        // Pass 2: semantic dedup (async → run synchronously in dedup context)
        return DeduplicateSemanticAsync(hashFiltered, existingScenarios).GetAwaiter().GetResult();
    }

    private async Task<IReadOnlyList<NeuronScenario>> DeduplicateSemanticAsync(
        IReadOnlyList<NeuronScenario> candidates,
        IReadOnlyList<NeuronScenario> existingScenarios)
    {
        // Build existing embeddings
        List<float[]> existingEmbeddings = [];
        foreach (NeuronScenario existing in existingScenarios)
        {
            float[] emb = await GetOrComputeEmbeddingAsync(existing);
            if (emb.Length > 0)
                existingEmbeddings.Add(emb);
        }

        // If no existing embeddings, can't do semantic dedup
        if (existingEmbeddings.Count == 0)
            return candidates;

        List<NeuronScenario> result = [];
        List<float[]> acceptedEmbeddings = new(existingEmbeddings);

        foreach (NeuronScenario candidate in candidates)
        {
            float[] candidateEmb = await GetOrComputeEmbeddingAsync(candidate);

            if (candidateEmb.Length == 0)
            {
                // Can't embed → keep (fall back to hash-only)
                result.Add(candidate);
                continue;
            }

            // Check cosine similarity against all existing + already accepted
            bool isDuplicate = false;
            foreach (float[] existingEmb in acceptedEmbeddings)
            {
                double similarity = CosineSimilarity(candidateEmb, existingEmb);
                if (similarity >= options.SemanticDedupThreshold)
                {
                    isDuplicate = true;
                    logger?.Debug("Semantic dedup: '{Scenario}' similarity {Sim:F3} >= threshold {Thresh}",
                        candidate.ScenarioName, similarity, options.SemanticDedupThreshold);
                    break;
                }
            }

            if (!isDuplicate)
            {
                result.Add(candidate);
                acceptedEmbeddings.Add(candidateEmb);
            }
        }

        if (candidates.Count != result.Count)
        {
            logger?.Info("Semantic dedup: {Removed}/{Total} scenarios removed as semantically similar",
                candidates.Count - result.Count, candidates.Count);
        }

        return result;
    }

    private async Task<float[]> GetOrComputeEmbeddingAsync(NeuronScenario scenario)
    {
        string key = scenario.Id;
        if (_embeddingCache.TryGetValue(key, out float[]? cached))
            return cached;

        string text = $"{scenario.ScenarioName} | {scenario.ExpectedBehavior ?? ""}";
        float[] embedding = await embedder.EmbedAsync(text);
        _embeddingCache.TryAdd(key, embedding);
        return embedding;
    }

    internal static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }
}
