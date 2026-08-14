namespace Muonroi.RuleGen.Commands;

internal static class GenerateTestsCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        string? rules = OptionReader.GetString(context, "rules");
        string? output = OptionReader.GetString(context, "output");

        if (string.IsNullOrWhiteSpace(rules) || string.IsNullOrWhiteSpace(output))
        {
            MGuard.Fail<object>("Missing required options --rules and --output.");
        }

        string rulesDir = Path.GetFullPath(rules, context.WorkingDirectory);
        string outputDir = Path.GetFullPath(output, context.WorkingDirectory);
        string ns = OptionReader.GetString(context, "namespace", context.Config.Extract.Namespace) ?? "Generated.Rules";

        Directory.CreateDirectory(outputDir);
        IReadOnlyList<Models.DiscoveredRuleType> discovered = RuleTypeDiscoveryService.Discover(rulesDir);

        foreach (Models.DiscoveredRuleType rule in discovered)
        {
            string content = TestScaffoldWriter.Render(rule, $"{ns}.Tests");
            string path = Path.Combine(outputDir, $"{rule.ClassName}Tests.cs");
            await File.WriteAllTextAsync(path, content);
        }

        Console.WriteLine($"Generated {discovered.Count} test scaffold(s) into '{outputDir}'.");
        return 0;
    }
}
