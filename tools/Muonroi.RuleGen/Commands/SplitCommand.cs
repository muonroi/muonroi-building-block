using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Services;
using Muonroi.RuleGen.Writers;
using Spectre.Console;

namespace Muonroi.RuleGen.Commands;

internal static class SplitCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        string? source = OptionReader.GetString(context, "source");
        string? outputDir = OptionReader.GetString(context, "output-dir");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(outputDir))
        {
            throw new InvalidOperationException("Missing required options --source and --output-dir.");
        }

        string workflow = OptionReader.GetString(context, "workflow", "split-workflow")!;
        string? exportJson = OptionReader.GetString(context, "export-json");
        string? tenant = OptionReader.GetString(context, "tenant");
        string? classTarget = OptionReader.GetString(context, "class");
        string? versionText = OptionReader.GetString(context, "version", "1");
        int version = int.TryParse(versionText, out int parsedVersion) && parsedVersion > 0 ? parsedVersion : 1;

        string ns = OptionReader.GetString(context, "namespace", context.Config.Extract.Namespace) ?? "Generated.Rules";
        string? contextType = OptionReader.GetString(context, "context", context.Config.Extract.ContextType);
        string pattern = OptionReader.GetString(context, "pattern", context.Config.Extract.Pattern) ?? "**/*.cs";
        IReadOnlyList<string> excludes = OptionReader.GetCsvList(context, "exclude", context.Config.Extract.ExcludePatterns);
        bool parallel = OptionReader.GetBool(context, "parallel", context.Config.Extract.Parallel);

        return await AnsiConsole.Status()
            .StartAsync("[yellow]Splitting rules...[/]", async ctx =>
            {
                try
                {
                    IReadOnlyList<string> files = SourceDiscoveryService.Discover(
                        context.WorkingDirectory,
                        source,
                        null,
                        OptionReader.GetString(context, "project"),
                        pattern,
                        excludes);

                    IReadOnlyList<ExtractedRuleDefinition> extracted = await RoslynRuleExtractor.ExtractAsync(files, ns, contextType, parallel, CancellationToken.None);
                    IReadOnlyList<ExtractedRuleDefinition> definitions = ApplyClassFilter(extracted, classTarget);

                    if (definitions.Count == 0)
                    {
                        CommandLineWriter.WriteInfo("No [MExtractAsRule] methods found to split.");
                        return 0;
                    }

                    string outputPath = Path.GetFullPath(outputDir, context.WorkingDirectory);
                    Directory.CreateDirectory(outputPath);

                    string author = AuditMetadataService.ResolveAuthor();
                    string? commit = AuditMetadataService.ResolveGitCommit(context.WorkingDirectory);
                    foreach (ExtractedRuleDefinition definition in definitions)
                    {
                        string rendered = RuleClassWriter.Render(definition, tenant, author, commit);
                        string file = Path.Combine(outputPath, $"{RuleClassWriter.ToIdentifier(definition.Code)}.g.cs");
                        await File.WriteAllTextAsync(file, rendered);
                    }

                    if (!string.IsNullOrWhiteSpace(exportJson))
                    {
                        RuntimeRuleDefinition[] runtimeRules = [.. definitions
                            .Select(def =>
                            {
                                (string conditionFeel, string actionFeel, bool isCustom) = CSharpRulePatternService.ExtractConditionAndAction(def.MethodBody);
                                return new RuntimeRuleDefinition(
                                    def.Code,
                                    def.MethodName,
                                    def.Order,
                                    def.HookPoint,
                                    def.DependsOn,
                                    conditionFeel,
                                    actionFeel,
                                    "Validation",
                                    isCustom ? "custom" : "generated");
                            })];

                        string json = RuntimeRuleJsonService.Export(workflow, version, tenant, runtimeRules);
                        string exportPath = Path.GetFullPath(exportJson, context.WorkingDirectory);
                        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
                        await File.WriteAllTextAsync(exportPath, json);
                        CommandLineWriter.WriteSuccess($"Exported runtime JSON: '{exportPath}'.");
                    }

                    CommandLineWriter.WriteSuccess($"Split completed. Generated {definitions.Count} rule file(s) into '{outputPath}'.");
                    return 0;
                }
                catch (Exception ex)
                {
                    CommandLineWriter.WriteError($"Split failed: {ex.Message}");
                    return 1;
                }
            });
    }

    private static IReadOnlyList<ExtractedRuleDefinition> ApplyClassFilter(
        IReadOnlyList<ExtractedRuleDefinition> definitions,
        string? classTarget)
    {
        if (string.IsNullOrWhiteSpace(classTarget))
        {
            return definitions;
        }

        return definitions
            .Where(d =>
                string.Equals(d.ClassName, classTarget, StringComparison.OrdinalIgnoreCase) ||
                string.Equals($"{d.SourceNamespace}.{d.ClassName}", classTarget, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
