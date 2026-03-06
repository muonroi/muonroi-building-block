using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Services;
using Muonroi.RuleGen.Writers;

namespace Muonroi.RuleGen.Commands;

internal static class VerifyCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        string? source = OptionReader.GetString(context, "source", context.Config.Extract.Source);
        string? sourceDir = OptionReader.GetString(context, "source-dir", context.Config.Extract.SourceDir);
        string? project = OptionReader.GetString(context, "project", context.Config.Extract.Project);
        string pattern = OptionReader.GetString(context, "pattern", context.Config.Extract.Pattern) ?? "**/*.cs";
        IReadOnlyList<string> excludes = OptionReader.GetCsvList(context, "exclude", context.Config.Extract.ExcludePatterns);
        string ns = OptionReader.GetString(context, "namespace", context.Config.Extract.Namespace) ?? "Generated.Rules";
        string? contextType = OptionReader.GetString(context, "context", context.Config.Extract.ContextType);
        bool parallel = OptionReader.GetBool(context, "parallel", context.Config.Extract.Parallel);

        string? rulesPath = OptionReader.GetString(context, "rules");
        if (string.IsNullOrWhiteSpace(rulesPath))
        {
            throw new InvalidOperationException("Missing required option --rules.");
        }

        string resolvedRulesPath = Path.GetFullPath(rulesPath, context.WorkingDirectory);
        if (!Directory.Exists(resolvedRulesPath))
        {
            throw new DirectoryNotFoundException($"Rules directory not found: {resolvedRulesPath}");
        }

        IReadOnlyList<string> files = SourceDiscoveryService.Discover(
            context.WorkingDirectory,
            source,
            sourceDir,
            project,
            pattern,
            excludes);

        RoslynRuleExtractor extractor = new();
        IReadOnlyList<Models.ExtractedRuleDefinition> definitions = await RoslynRuleExtractor.ExtractAsync(
            files,
            ns,
            contextType,
            parallel,
            CancellationToken.None);

        HashSet<string> expected = definitions
            .Select(d => $"{RuleClassWriter.ToIdentifier(d.Code)}.g.cs")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> actual = Directory.GetFiles(resolvedRulesPath, "*.g.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] missing = [.. expected.Except(actual, StringComparer.OrdinalIgnoreCase).OrderBy(x => x)];
        if (missing.Length > 0)
        {
            Console.Error.WriteLine("Missing generated rule files:");
            foreach (string? file in missing)
            {
                Console.Error.WriteLine($"- {file}");
            }

            return 2;
        }

        Console.WriteLine($"Verify passed. Found {expected.Count} expected generated file(s).");
        return 0;
    }
}
