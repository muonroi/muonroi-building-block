using Muonroi.RuleGen.Models;
using System.Text.Json;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleGen.Services;

internal static class RuntimeRuleJsonService
{
    public static RuntimeRuleSet Load(string path, string? defaultWorkflow, string? tenantId)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Rules JSON file not found: {path}");
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = doc.RootElement;

        string workflow = defaultWorkflow ?? "runtime-workflow";
        int version = 1;
        string? tenant = tenantId;
        List<RuntimeRuleDefinition> rules = [];

        if (root.ValueKind == JsonValueKind.Object)
        {
            workflow = TryGetString(root, "workflowName")
                ?? TryGetString(root, "WorkflowName")
                ?? workflow;
            version = TryGetInt(root, "version") ?? TryGetInt(root, "Version") ?? 1;
            tenant ??= TryGetString(root, "tenantId") ?? TryGetString(root, "TenantId");

            if (TryGetArray(root, "rules", out JsonElement rulesArray) || TryGetArray(root, "Rules", out rulesArray))
            {
                rules.AddRange(ParseRules(rulesArray));
            }
            else
            {
                rules.AddRange(ParseRules(root));
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            rules.AddRange(ParseRules(root));
        }
        else
        {
            throw new MConfigurationException("Runtime rules JSON must be an object or array.");
        }

        if (rules.Count == 0)
        {
            throw new MConfigurationException("Runtime rules JSON does not contain any rules.");
        }

        return new RuntimeRuleSet(workflow, version, tenant, rules);
    }

    public static string Export(
        string workflowName,
        int version,
        string? tenantId,
        IReadOnlyList<RuntimeRuleDefinition> rules)
    {
        var payload = new
        {
            workflowName,
            version,
            tenantId,
            rules = rules.Select(r => new
            {
                code = r.Code,
                name = r.Name,
                order = r.Order,
                hookPoint = r.HookPoint,
                dependsOn = r.DependsOn,
                condition = r.Condition,
                action = r.Action,
                type = r.Type,
                source = r.Source
            })
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static IEnumerable<RuntimeRuleDefinition> ParseRules(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? code = item.GetString();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    yield return new RuntimeRuleDefinition(code, code, 0, "BeforeRule", [], null, null, "Validation", "legacy-string");
                }

                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string codeValue = TryGetString(item, "code")
                ?? TryGetString(item, "Code")
                ?? TryGetString(item, "RuleName")
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(codeValue))
            {
                continue;
            }

            string name = TryGetString(item, "name")
                ?? TryGetString(item, "Name")
                ?? codeValue;

            int order = TryGetInt(item, "order") ?? TryGetInt(item, "Order") ?? 0;
            string hook = TryGetString(item, "hookPoint")
                ?? TryGetString(item, "HookPoint")
                ?? "BeforeRule";
            string type = TryGetString(item, "type") ?? TryGetString(item, "Type") ?? "Validation";

            List<string> deps = [];
            if (TryGetArray(item, "dependsOn", out JsonElement depArr) || TryGetArray(item, "DependsOn", out depArr))
            {
                deps.AddRange(depArr.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>());
            }

            string? condition = TryGetString(item, "condition")
                ?? TryGetString(item, "Condition")
                ?? TryGetString(item, "Expression");
            string? action = TryGetString(item, "action")
                ?? TryGetString(item, "Action");

            if (string.IsNullOrWhiteSpace(action) &&
                item.TryGetProperty("Actions", out JsonElement actionsEl) &&
                actionsEl.ValueKind == JsonValueKind.Object)
            {
                action = TryGetString(actionsEl, "OnSuccess") ?? TryGetString(actionsEl, "onSuccess");
            }

            yield return new RuntimeRuleDefinition(
                codeValue,
                name,
                order,
                hook,
                deps,
                condition,
                action,
                type,
                TryGetString(item, "source") ?? TryGetString(item, "Source"));
        }
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ValueKind == JsonValueKind.Number ? value.ToString() : null;
    }

    private static int? TryGetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool TryGetArray(JsonElement element, string property, out JsonElement array)
    {
        if (element.TryGetProperty(property, out JsonElement candidate) && candidate.ValueKind == JsonValueKind.Array)
        {
            array = candidate;
            return true;
        }

        array = default;
        return false;
    }
}
