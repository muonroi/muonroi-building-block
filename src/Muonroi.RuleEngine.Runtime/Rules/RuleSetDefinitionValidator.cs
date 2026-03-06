namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleSetDefinitionValidator : IRuleSetDefinitionValidator
{
    public RuleSetValidationResult Validate(string workflowName, string json)
    {
        RuleSetValidationResult result = new()
        {
            WorkflowName = workflowName
        };

        if (string.IsNullOrWhiteSpace(workflowName))
        {
            result.Errors.Add(new RuleSetValidationIssue("WorkflowRequired", "Workflow name is required.", "$.workflowName"));
            return result;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            result.Errors.Add(new RuleSetValidationIssue("PayloadEmpty", "Ruleset payload is empty.", "$"));
            return result;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            result.Errors.Add(new RuleSetValidationIssue("InvalidJson", $"Ruleset payload is not valid JSON: {ex.Message}", "$"));
            return result;
        }

        using (doc)
        {
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array && root.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add(new RuleSetValidationIssue("InvalidRootKind", "Ruleset payload must be a JSON object or array.", "$"));
                return result;
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                ValidateArrayShape(workflowName, root, result);
                return result;
            }

            ValidateObjectShape(workflowName, root, result);
            return result;
        }
    }

    private static void ValidateArrayShape(string workflowName, JsonElement root, RuleSetValidationResult result)
    {
        if (root.GetArrayLength() == 0)
        {
            result.Errors.Add(new RuleSetValidationIssue("EmptyArray", "Ruleset array is empty.", "$"));
            return;
        }

        JsonElement first = root[0];
        if (first.ValueKind == JsonValueKind.String)
        {
            result.Shape = "CodeArray";
            return;
        }

        if (first.ValueKind != JsonValueKind.Object)
        {
            result.Errors.Add(new RuleSetValidationIssue("InvalidArrayItem", "Ruleset array items must be object or string.", "$[0]"));
            return;
        }

        string? workflowFromPayload = TryGetString(first, "WorkflowName");
        if (workflowFromPayload is not null &&
            !string.Equals(workflowFromPayload, workflowName, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add(new RuleSetValidationIssue(
                "WorkflowMismatch",
                $"WorkflowName mismatch. Expected '{workflowName}', payload has '{workflowFromPayload}'.",
                "$[0].WorkflowName"));
        }

        if (!first.TryGetProperty("Rules", out JsonElement rules) || rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() == 0)
        {
            result.Errors.Add(new RuleSetValidationIssue("RulesMissing", "Rules collection must be a non-empty array.", "$[0].Rules"));
            return;
        }

        result.Shape = "LegacyWorkflowArray";
    }

    private static void ValidateObjectShape(string workflowName, JsonElement root, RuleSetValidationResult result)
    {
        string? workflowFromPayload = TryGetString(root, "workflowName") ?? TryGetString(root, "WorkflowName");
        if (!string.IsNullOrWhiteSpace(workflowFromPayload) &&
            !string.Equals(workflowFromPayload, workflowName, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add(new RuleSetValidationIssue(
                "WorkflowMismatch",
                $"WorkflowName mismatch. Expected '{workflowName}', payload has '{workflowFromPayload}'.",
                "$.workflowName"));
        }

        if (TryGetArray(root, "rules", out JsonElement rules) || TryGetArray(root, "Rules", out rules))
        {
            if (rules.GetArrayLength() == 0)
            {
                result.Errors.Add(new RuleSetValidationIssue("RulesEmpty", "Rules collection must be a non-empty array.", "$.rules"));
                return;
            }

            JsonElement first = rules[0];
            if (first.ValueKind == JsonValueKind.String)
            {
                result.Shape = "CodeWorkflowObject";
                return;
            }

            if (first.ValueKind == JsonValueKind.Object)
            {
                result.Shape = "LegacyWorkflowObject";
                return;
            }

            result.Errors.Add(new RuleSetValidationIssue("RulesItemType", "Rules[] items must be either string or object.", "$.rules[0]"));
            return;
        }

        if (root.TryGetProperty("RuleName", out _))
        {
            result.Shape = "LegacyRuleObject";
            return;
        }

        result.Errors.Add(new RuleSetValidationIssue(
            "RulesMissing",
            "Cannot find rules definition. Expected property 'rules'/'Rules' or legacy workflow object.",
            "$.rules"));
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool TryGetArray(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out JsonElement candidate) && candidate.ValueKind == JsonValueKind.Array)
        {
            value = candidate;
            return true;
        }

        value = default;
        return false;
    }
}
