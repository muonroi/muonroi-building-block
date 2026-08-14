namespace Muonroi.RuleGen.Services;

internal static class RuleValidationService
{
    public static ValidationReport Validate(IReadOnlyList<ExtractedRuleDefinition> rules)
    {
        ValidationReport report = new();

        foreach (IGrouping<string, ExtractedRuleDefinition> group in rules.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            string locations = string.Join(", ", group.Select(x => $"{Path.GetFileName(x.SourceFile)}:{x.SourceLine}"));
            report.Errors.Add($"Duplicate rule code '{group.Key}' at {locations}");
        }

        foreach (ExtractedRuleDefinition rule in rules)
        {
            if (!Enum.TryParse(rule.HookPoint, ignoreCase: true, out HookPoint _))
            {
                report.Errors.Add($"Rule '{rule.Code}' has invalid HookPoint '{rule.HookPoint}'.");
            }
        }

        Dictionary<string, ExtractedRuleDefinition> byCode = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExtractedRuleDefinition rule in rules)
        {
            byCode.TryAdd(rule.Code, rule);
        }
        foreach (ExtractedRuleDefinition rule in rules)
        {
            foreach (string dep in rule.DependsOn)
            {
                if (!byCode.ContainsKey(dep))
                {
                    report.Warnings.Add($"Rule '{rule.Code}' depends on missing rule '{dep}'.");
                }
            }
        }

        DetectCycles(rules, report);
        return report;
    }

    private static void DetectCycles(IReadOnlyList<ExtractedRuleDefinition> rules, ValidationReport report)
    {
        Dictionary<string, IReadOnlyList<string>> graph = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExtractedRuleDefinition rule in rules)
        {
            graph.TryAdd(rule.Code, rule.DependsOn);
        }

        HashSet<string> visiting = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Stack<string> stack = new();

        foreach (ExtractedRuleDefinition rule in rules)
        {
            Visit(rule.Code);
        }

        void Visit(string node)
        {
            if (visited.Contains(node))
            {
                return;
            }

            if (visiting.Contains(node))
            {
                IEnumerable<string> path = stack.Reverse().Concat([node]);
                report.Errors.Add($"Circular dependency detected: {string.Join(" -> ", path)}");
                return;
            }

            visiting.Add(node);
            stack.Push(node);

            if (graph.TryGetValue(node, out IReadOnlyList<string>? deps))
            {
                foreach (string dep in deps)
                {
                    if (graph.ContainsKey(dep))
                    {
                        Visit(dep);
                    }
                }
            }

            stack.Pop();
            visiting.Remove(node);
            visited.Add(node);
        }
    }
}
