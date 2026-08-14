namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Parses AI-generated JSON responses into <see cref="NeuronScenario"/> instances.
/// Shared across all brain providers.
/// </summary>
public static class ScenarioParser
{
    /// <summary>
    /// Retry prompts appended on each retry to escalate instructions to the AI.
    /// Index 0 = retry 1, Index 1 = retry 2, Index 2 = retry 3.
    /// </summary>
    private static readonly string[] RetryPromptSuffixes =
    [
        "\n\nIMPORTANT: You MUST return at least 1 scenario as a JSON array. Do not return an empty array.",
        "\n\nReturn EXACTLY this JSON structure: [{\"scenario\":\"...\",\"type\":\"technical\",\"reason\":\"...\",\"inputFacts\":{},\"expectedBehavior\":\"...\"}]. Do NOT return empty.",
        "\n\nGenerate simple boundary test cases. Return a JSON array with at least one item covering a basic input value."
    ];

    /// <summary>
    /// Parse with retry: calls <paramref name="aiCall"/> up to <paramref name="maxRetries"/> times.
    /// If all retries produce 0 scenarios, falls back to <paramref name="syntheticGen"/>.
    /// Guarantees at least 1 scenario is returned.
    /// </summary>
    public static async Task<IReadOnlyList<NeuronScenario>> ParseWithRetry(
        Func<string, Task<string?>> aiCall,
        string seedRuleCode,
        ProliferationContext context,
        ISyntheticScenarioGenerator syntheticGen,
        RuleSetSchema schema,
        int maxRetries = 3)
    {
        // Initial call with original prompt (empty suffix = no modification)
        string? response = await aiCall(string.Empty);
        if (response is not null)
        {
            IReadOnlyList<NeuronScenario> parsed = Parse(response, seedRuleCode, context);
            if (parsed.Count > 0) return parsed;
        }

        // Retry with escalating prompt variations
        for (int i = 0; i < maxRetries && i < RetryPromptSuffixes.Length; i++)
        {
            string retrySuffix = RetryPromptSuffixes[i];
            string? retryResponse = await aiCall(retrySuffix);
            if (retryResponse is not null)
            {
                IReadOnlyList<NeuronScenario> retryParsed = Parse(retryResponse, seedRuleCode, context);
                if (retryParsed.Count > 0) return retryParsed;
            }
        }

        // All retries exhausted — use synthetic fallback
        IReadOnlyList<NeuronScenario> synthetic = syntheticGen.Generate(seedRuleCode, schema, context);
        return synthetic;
    }

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
