using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Parses AI-generated JSON responses into <see cref="NeuronScenario"/> instances.
/// Shared across all brain providers.
/// </summary>
public static class ScenarioParser
{
    /// <summary>
    /// Parse an AI response string (JSON array) into a list of scenarios.
    /// Handles markdown code fences, skips incomplete entries.
    /// </summary>
    public static IReadOnlyList<NeuronScenario> Parse(
        string aiResponse,
        string seedRuleCode,
        ProliferationContext context)
    {
        // Strip markdown code fences if AI wraps response
        string json = aiResponse.Trim();
        if (json.StartsWith("```"))
        {
            int firstNewline = json.IndexOf('\n');
            if (firstNewline > 0) json = json[(firstNewline + 1)..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            List<NeuronScenario> scenarios = [];

            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                string? name = item.TryGetProperty("scenario", out JsonElement nameEl) ? nameEl.GetString() : null;
                string? typeStr = item.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : null;
                string? reason = item.TryGetProperty("reason", out JsonElement reasonEl) ? reasonEl.GetString() : null;
                JsonElement inputFacts = item.TryGetProperty("inputFacts", out JsonElement inputEl)
                    ? inputEl.Clone()
                    : default;
                string? expected = item.TryGetProperty("expectedBehavior", out JsonElement expEl)
                    ? expEl.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(reason)) continue;

                ScenarioType type = typeStr?.Equals("technical", StringComparison.OrdinalIgnoreCase) == true
                    ? ScenarioType.Technical
                    : ScenarioType.Business;

                scenarios.Add(new NeuronScenario
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SeedRuleCode = seedRuleCode,
                    ScenarioName = name,
                    Type = type,
                    Scope = context.Scope,
                    ParentScenarioId = null,
                    GenerationDepth = context.CurrentDepth,
                    ProliferationReason = reason,
                    InputFacts = inputFacts,
                    ExpectedBehavior = expected,
                    Status = ScenarioStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow,
                    TenantId = context.TenantId
                });
            }

            return scenarios;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
