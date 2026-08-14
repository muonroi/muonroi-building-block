namespace Muonroi.Experience.Runtime.Evolution;

/// <summary>
/// Text-based Jaccard clustering for Tier 2 experience entries.
/// Single-linkage O(n^2) algorithm — acceptable for collection sizes under 1000 entries.
/// </summary>
internal static class ClusteringEngine
{
    /// <summary>
    /// Groups entries into clusters where any pair sharing the seed entry has Jaccard similarity
    /// above <paramref name="threshold"/> on their combined Trigger+Question tokens.
    /// </summary>
    /// <param name="entries">All Tier 2 entries to cluster.</param>
    /// <param name="threshold">Minimum Jaccard similarity to include in the same cluster (e.g., 0.7).</param>
    /// <returns>List of clusters; each cluster is a list of related entries.</returns>
    public static List<List<NeuronExperience>> GroupBySimilarity(
        IReadOnlyList<NeuronExperience> entries,
        float threshold)
    {
        var clusters = new List<List<NeuronExperience>>();
        var assigned = new HashSet<string>(StringComparer.Ordinal);

        foreach (NeuronExperience entry in entries)
        {
            if (assigned.Contains(entry.Id)) continue;

            var cluster = new List<NeuronExperience> { entry };
            assigned.Add(entry.Id);

            foreach (NeuronExperience other in entries)
            {
                if (assigned.Contains(other.Id)) continue;

                if (ComputeJaccard(entry, other) >= threshold)
                {
                    cluster.Add(other);
                    assigned.Add(other.Id);
                }
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    private static float ComputeJaccard(NeuronExperience a, NeuronExperience b)
    {
        HashSet<string> setA = Tokenize(a.Trigger + " " + a.Question);
        HashSet<string> setB = Tokenize(b.Trigger + " " + b.Question);

        if (setA.Count == 0 && setB.Count == 0) return 0f;

        int intersectionCount = 0;
        foreach (string token in setA)
        {
            if (setB.Contains(token)) intersectionCount++;
        }

        int unionCount = setA.Count + setB.Count - intersectionCount;
        return unionCount == 0 ? 0f : (float)intersectionCount / unionCount;
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', '?', '!'))
            .Where(w => w.Length > 2)
            .ToHashSet(StringComparer.Ordinal);
}
