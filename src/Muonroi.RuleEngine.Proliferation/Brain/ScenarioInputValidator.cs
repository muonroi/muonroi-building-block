using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Validation result for a scenario's input facts against the ruleset schema.
/// </summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>
/// Validates scenario input facts against extracted ruleset schema.
/// Used before execution to skip scenarios with invalid/hallucinated inputs.
/// </summary>
public static class ScenarioInputValidator
{
    /// <summary>
    /// Validate that a scenario's inputFacts match the expected schema.
    /// </summary>
    public static ValidationResult Validate(NeuronScenario scenario, RuleSetSchema schema)
    {
        // If schema has no input fields, skip validation (can't validate without schema)
        if (schema.InputFields.Count == 0)
            return new ValidationResult(true, []);

        List<string> errors = [];

        // Check that inputFacts is a valid JSON object
        if (scenario.InputFacts.ValueKind != JsonValueKind.Object)
        {
            errors.Add("inputFacts is not a JSON object");
            return new ValidationResult(false, errors);
        }

        // Collect input fact keys
        HashSet<string> factKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty prop in scenario.InputFacts.EnumerateObject())
        {
            factKeys.Add(prop.Name);
        }

        // Check required fields are present
        foreach (FieldSchema field in schema.InputFields)
        {
            if (field.IsRequired && !factKeys.Contains(field.Name))
            {
                errors.Add($"Missing required field: {field.Name}");
            }
        }

        // Check for unknown fields (hallucinated by AI)
        HashSet<string> schemaFieldNames = new(
            schema.InputFields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        int unknownCount = 0;
        foreach (string key in factKeys)
        {
            if (!schemaFieldNames.Contains(key))
            {
                unknownCount++;
                errors.Add($"Unknown field: {key} (not in schema)");
            }
        }

        // Skip scenarios with >50% unknown fields (likely hallucinated)
        if (factKeys.Count > 0 && unknownCount > factKeys.Count / 2)
        {
            errors.Insert(0, $"Too many unknown fields ({unknownCount}/{factKeys.Count}): likely hallucinated inputs");
            return new ValidationResult(false, errors);
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}
