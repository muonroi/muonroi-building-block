using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Deduplicates scenarios by comparing SHA256 hashes of normalized input facts.
/// Also detects name-based duplicates (case-insensitive, punctuation-stripped).
/// </summary>
public sealed class InputHashDeduplicator : IScenarioDeduplicator
{
    /// <summary>Removes duplicate scenarios based on input hash and normalized name.</summary>
    public IReadOnlyList<NeuronScenario> Deduplicate(
        IReadOnlyList<NeuronScenario> candidates,
        IReadOnlyList<NeuronScenario> existingScenarios)
    {
        if (candidates.Count == 0) return candidates;

        // Build sets from existing scenarios
        HashSet<string> existingInputHashes = [];
        HashSet<string> existingNameHashes = [];

        foreach (NeuronScenario existing in existingScenarios)
        {
            string inputHash = ComputeInputHash(existing.InputFacts);
            if (!string.IsNullOrEmpty(inputHash))
                existingInputHashes.Add(inputHash);

            string nameHash = NormalizeName(existing.ScenarioName);
            if (!string.IsNullOrEmpty(nameHash))
                existingNameHashes.Add(nameHash);
        }

        // Track hashes within candidates to avoid intra-batch duplicates
        HashSet<string> seenInputHashes = new(existingInputHashes);
        HashSet<string> seenNameHashes = new(existingNameHashes);
        List<NeuronScenario> unique = [];

        foreach (NeuronScenario candidate in candidates)
        {
            string inputHash = ComputeInputHash(candidate.InputFacts);
            string nameHash = NormalizeName(candidate.ScenarioName);

            // Skip if exact input duplicate
            if (!string.IsNullOrEmpty(inputHash) && !seenInputHashes.Add(inputHash))
                continue;

            // Skip if name-normalized duplicate
            if (!string.IsNullOrEmpty(nameHash) && !seenNameHashes.Add(nameHash))
                continue;

            unique.Add(candidate);
        }

        return unique;
    }

    /// <summary>Computes a stable hash for the supplied input facts.</summary>
    internal static string ComputeInputHash(JsonElement inputFacts)
    {
        if (inputFacts.ValueKind == JsonValueKind.Undefined)
            return string.Empty;

        try
        {
            // Normalize: serialize with sorted keys for consistent hashing
            string normalized = NormalizeJson(inputFacts);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Normalizes a scenario name for duplicate detection.</summary>
    internal static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        StringBuilder sb = new();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }

        return sb.ToString();
    }

    private static string NormalizeJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => NormalizeObject(element),
            JsonValueKind.Array => NormalizeArray(element),
            _ => element.GetRawText()
        };
    }

    private static string NormalizeObject(JsonElement obj)
    {
        // Sort properties by key for deterministic hashing
        List<KeyValuePair<string, string>> sorted = [];
        foreach (JsonProperty prop in obj.EnumerateObject())
        {
            sorted.Add(new(prop.Name, NormalizeJson(prop.Value)));
        }

        sorted.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

        StringBuilder sb = new();
        sb.Append('{');
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(sorted[i].Key).Append("\":").Append(sorted[i].Value);
        }
        sb.Append('}');

        return sb.ToString();
    }

    private static string NormalizeArray(JsonElement arr)
    {
        StringBuilder sb = new();
        sb.Append('[');
        int i = 0;
        foreach (JsonElement item in arr.EnumerateArray())
        {
            if (i > 0) sb.Append(',');
            sb.Append(NormalizeJson(item));
            i++;
        }
        sb.Append(']');

        return sb.ToString();
    }
}
