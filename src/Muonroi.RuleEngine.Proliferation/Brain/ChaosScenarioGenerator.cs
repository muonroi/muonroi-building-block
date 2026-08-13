using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Generates intentionally adversarial scenarios for resilience testing.
/// </summary>
public interface IChaosScenarioGenerator
{
    /// <summary>Generates chaos scenarios for a ruleset.</summary>
    IReadOnlyList<NeuronScenario> GenerateChaosScenarios(
        string seedRuleCode,
        string ruleSetJson,
        RuleSetSchema schema,
        int budget);
}

/// <summary>
/// Default chaos generator: null injection, overflow values, type coercion,
/// malformed inputs, cross-tenant boundary tests.
/// </summary>
public sealed class DefaultChaosScenarioGenerator : IChaosScenarioGenerator
{
    /// <summary>Generates chaos scenarios for a ruleset.</summary>
    public IReadOnlyList<NeuronScenario> GenerateChaosScenarios(
        string seedRuleCode,
        string ruleSetJson,
        RuleSetSchema schema,
        int budget)
    {
        if (budget <= 0 || schema.InputFields.Count == 0)
            return [];

        List<NeuronScenario> scenarios = [];

        // 1. Null injection: null for each required field
        foreach (FieldSchema field in schema.InputFields.Where(f => f.IsRequired))
        {
            if (scenarios.Count >= budget) break;
            scenarios.Add(CreateChaosScenario(
                seedRuleCode,
                $"Chaos: null {field.Name}",
                $"Null injection for required field '{field.Name}' — should fail validation",
                BuildInputWithNull(schema, field.Name)));
        }

        // 2. Overflow values for numeric fields
        foreach (FieldSchema field in schema.InputFields.Where(f =>
            f.DataType.Equals("number", StringComparison.OrdinalIgnoreCase) ||
            f.DataType.Equals("integer", StringComparison.OrdinalIgnoreCase) ||
            f.DataType.Equals("int", StringComparison.OrdinalIgnoreCase) ||
            f.DataType.Equals("decimal", StringComparison.OrdinalIgnoreCase)))
        {
            if (scenarios.Count >= budget) break;
            scenarios.Add(CreateChaosScenario(
                seedRuleCode,
                $"Chaos: overflow {field.Name}",
                $"Extreme numeric value for '{field.Name}' — tests overflow handling",
                BuildInputWithOverflow(schema, field.Name)));
        }

        // 3. Type coercion: string where number expected
        foreach (FieldSchema field in schema.InputFields.Where(f =>
            f.DataType.Equals("number", StringComparison.OrdinalIgnoreCase) ||
            f.DataType.Equals("integer", StringComparison.OrdinalIgnoreCase)))
        {
            if (scenarios.Count >= budget) break;
            scenarios.Add(CreateChaosScenario(
                seedRuleCode,
                $"Chaos: type coercion {field.Name}",
                $"String value where number expected for '{field.Name}' — tests type handling",
                BuildInputWithWrongType(schema, field.Name)));
        }

        // 4. Empty string for string fields
        foreach (FieldSchema field in schema.InputFields.Where(f =>
            f.DataType.Equals("string", StringComparison.OrdinalIgnoreCase)))
        {
            if (scenarios.Count >= budget) break;
            scenarios.Add(CreateChaosScenario(
                seedRuleCode,
                $"Chaos: empty string {field.Name}",
                $"Empty string for '{field.Name}' — tests empty input handling",
                BuildInputWithEmptyString(schema, field.Name)));
        }

        // 5. All fields null
        if (scenarios.Count < budget)
        {
            scenarios.Add(CreateChaosScenario(
                seedRuleCode,
                "Chaos: all fields null",
                "All input fields set to null — tests complete absence of data",
                BuildAllNull(schema)));
        }

        // 6. Extra unknown fields (hallucinated inputs)
        if (scenarios.Count < budget)
        {
            scenarios.Add(CreateChaosScenario(
                seedRuleCode,
                "Chaos: unknown fields injection",
                "Unknown field names injected — tests resilience to unexpected inputs",
                BuildWithUnknownFields(schema)));
        }

        return scenarios;
    }

    private static NeuronScenario CreateChaosScenario(
        string seedRuleCode, string name, string reason, JsonElement inputFacts)
    {
        return new NeuronScenario
        {
            Id = Guid.NewGuid().ToString("N"),
            SeedRuleCode = seedRuleCode,
            ScenarioName = name,
            Type = ScenarioType.Technical,
            Scope = ProliferationScope.Rule,
            GenerationDepth = 0,
            ProliferationReason = reason,
            InputFacts = inputFacts,
            ExpectedBehavior = "Should handle gracefully without crashing",
            Status = ScenarioStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static JsonElement BuildInputWithNull(RuleSetSchema schema, string nullFieldName)
    {
        Dictionary<string, object?> facts = [];
        foreach (FieldSchema field in schema.InputFields)
        {
            facts[field.Name] = field.Name.Equals(nullFieldName, StringComparison.OrdinalIgnoreCase)
                ? null
                : GetDefaultValue(field.DataType);
        }
        return SerializeToElement(facts);
    }

    private static JsonElement BuildInputWithOverflow(RuleSetSchema schema, string overflowFieldName)
    {
        Dictionary<string, object?> facts = [];
        foreach (FieldSchema field in schema.InputFields)
        {
            facts[field.Name] = field.Name.Equals(overflowFieldName, StringComparison.OrdinalIgnoreCase)
                ? (object)999999999999L
                : GetDefaultValue(field.DataType);
        }
        return SerializeToElement(facts);
    }

    private static JsonElement BuildInputWithWrongType(RuleSetSchema schema, string wrongTypeFieldName)
    {
        Dictionary<string, object?> facts = [];
        foreach (FieldSchema field in schema.InputFields)
        {
            facts[field.Name] = field.Name.Equals(wrongTypeFieldName, StringComparison.OrdinalIgnoreCase)
                ? (object)"not-a-number"
                : GetDefaultValue(field.DataType);
        }
        return SerializeToElement(facts);
    }

    private static JsonElement BuildInputWithEmptyString(RuleSetSchema schema, string emptyFieldName)
    {
        Dictionary<string, object?> facts = [];
        foreach (FieldSchema field in schema.InputFields)
        {
            facts[field.Name] = field.Name.Equals(emptyFieldName, StringComparison.OrdinalIgnoreCase)
                ? (object)""
                : GetDefaultValue(field.DataType);
        }
        return SerializeToElement(facts);
    }

    private static JsonElement BuildAllNull(RuleSetSchema schema)
    {
        Dictionary<string, object?> facts = [];
        foreach (FieldSchema field in schema.InputFields)
        {
            facts[field.Name] = null;
        }
        return SerializeToElement(facts);
    }

    private static JsonElement BuildWithUnknownFields(RuleSetSchema schema)
    {
        Dictionary<string, object?> facts = [];
        foreach (FieldSchema field in schema.InputFields)
        {
            facts[field.Name] = GetDefaultValue(field.DataType);
        }
        // Inject unknown fields
        facts["__chaos_unknown_field_1"] = "unexpected_value";
        facts["__chaos_unknown_field_2"] = 42;
        facts["__chaos_nested"] = new Dictionary<string, object> { ["inner"] = "value" };
        return SerializeToElement(facts);
    }

    private static object GetDefaultValue(string dataType) => dataType.ToLowerInvariant() switch
    {
        "number" or "integer" or "int" or "decimal" or "double" or "float" => 100,
        "boolean" or "bool" => true,
        _ => "test-value"
    };

    private static JsonElement SerializeToElement(Dictionary<string, object?> facts)
    {
        string json = JsonSerializer.Serialize(facts);
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
