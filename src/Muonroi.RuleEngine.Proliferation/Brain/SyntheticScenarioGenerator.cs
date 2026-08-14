namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Generates deterministic boundary scenarios from a ruleset schema without AI.
/// For each input field, creates scenarios covering: null, empty, min value, and max value.
/// Guarantees at least 1 scenario even if the schema has no fields.
/// </summary>
public sealed class SyntheticScenarioGenerator : ISyntheticScenarioGenerator
{
    private const string FallbackReason = "Synthetic boundary case (AI fallback)";

    /// <inheritdoc />
    public IReadOnlyList<NeuronScenario> Generate(
        string seedRuleCode,
        RuleSetSchema schema,
        ProliferationContext context)
    {
        List<NeuronScenario> scenarios = [];

        if (schema.InputFields.Count == 0)
        {
            // No schema fields — return a single empty-input scenario
            scenarios.Add(CreateScenario(seedRuleCode, context,
                name: "Empty input facts",
                facts: new Dictionary<string, object?>(),
                expectedBehavior: "Should handle empty input gracefully"));
            return scenarios;
        }

        foreach (FieldSchema field in schema.InputFields)
        {
            bool isNumeric = IsNumericType(field.DataType);

            // Null value boundary
            scenarios.Add(CreateScenario(seedRuleCode, context,
                name: $"Null {field.Name}",
                facts: new Dictionary<string, object?> { [field.Name] = null },
                expectedBehavior: $"Should handle null {field.Name}"));

            if (isNumeric)
            {
                // Min boundary (0)
                scenarios.Add(CreateScenario(seedRuleCode, context,
                    name: $"Min {field.Name} (zero)",
                    facts: new Dictionary<string, object?> { [field.Name] = 0 },
                    expectedBehavior: $"Should handle minimum value for {field.Name}"));

                // Max boundary (int.MaxValue)
                scenarios.Add(CreateScenario(seedRuleCode, context,
                    name: $"Max {field.Name}",
                    facts: new Dictionary<string, object?> { [field.Name] = int.MaxValue },
                    expectedBehavior: $"Should handle maximum value for {field.Name}"));

                // Negative boundary
                scenarios.Add(CreateScenario(seedRuleCode, context,
                    name: $"Negative {field.Name}",
                    facts: new Dictionary<string, object?> { [field.Name] = -1 },
                    expectedBehavior: $"Should handle negative value for {field.Name}"));
            }
            else
            {
                // Empty string boundary
                scenarios.Add(CreateScenario(seedRuleCode, context,
                    name: $"Empty {field.Name}",
                    facts: new Dictionary<string, object?> { [field.Name] = string.Empty },
                    expectedBehavior: $"Should handle empty string for {field.Name}"));

                // Very long string boundary
                scenarios.Add(CreateScenario(seedRuleCode, context,
                    name: $"Max length {field.Name}",
                    facts: new Dictionary<string, object?> { [field.Name] = new string('x', 1000) },
                    expectedBehavior: $"Should handle very long value for {field.Name}"));
            }
        }

        return scenarios;
    }

    private static NeuronScenario CreateScenario(
        string seedRuleCode,
        ProliferationContext context,
        string name,
        Dictionary<string, object?> facts,
        string expectedBehavior)
    {
        // Serialize facts dictionary to JsonElement
        string factsJson = JsonSerializer.Serialize(facts);
        using JsonDocument doc = JsonDocument.Parse(factsJson);
        JsonElement inputFacts = doc.RootElement.Clone();

        return new NeuronScenario
        {
            Id = Guid.NewGuid().ToString("N"),
            SeedRuleCode = seedRuleCode,
            ScenarioName = name,
            Type = ScenarioType.Technical,
            Scope = context.Scope,
            ParentScenarioId = null,
            GenerationDepth = context.CurrentDepth,
            ProliferationReason = FallbackReason,
            InputFacts = inputFacts,
            ExpectedBehavior = expectedBehavior,
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            TenantId = context.TenantId
        };
    }

    private static bool IsNumericType(string dataType) =>
        dataType.Equals("number", StringComparison.OrdinalIgnoreCase)
        || dataType.Equals("integer", StringComparison.OrdinalIgnoreCase)
        || dataType.Equals("int", StringComparison.OrdinalIgnoreCase)
        || dataType.Equals("decimal", StringComparison.OrdinalIgnoreCase)
        || dataType.Equals("double", StringComparison.OrdinalIgnoreCase)
        || dataType.Equals("float", StringComparison.OrdinalIgnoreCase)
        || dataType.Equals("long", StringComparison.OrdinalIgnoreCase);
}
