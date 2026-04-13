using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Services;
using Muonroi.RuleGen.Writers;
using Spectre.Console;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Muonroi.RuleGen.Commands;

internal static class ExtractCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        ExtractOptions options = ExtractOptions.FromContext(context);

        IReadOnlyList<string> sourceFiles = SourceDiscoveryService.Discover(
            context.WorkingDirectory,
            options.Source,
            options.SourceDir,
            options.Project,
            options.Pattern,
            options.ExcludePatterns);

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ExtractedRuleDefinition> definitions = [];

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Extracting rules[/]", maxValue: sourceFiles.Count);
                definitions = await RoslynRuleExtractor.ExtractAsync(
                    sourceFiles,
                    options.Namespace,
                    options.ContextType,
                    options.Parallel,
                    CancellationToken.None);
                task.Value = sourceFiles.Count;
            });

        if (definitions.Count == 0)
        {
            CommandLineWriter.WriteError("No methods marked with [MExtractAsRule] were found.");
            return 2;
        }

        ValidationReport? validation = null;
        if (options.Validate)
        {
            validation = RuleValidationService.Validate(definitions);
            foreach (string warning in validation.Warnings)
            {
                CommandLineWriter.WriteWarning(warning);
            }

            if (validation.HasErrors)
            {
                foreach (string error in validation.Errors)
                {
                    CommandLineWriter.WriteError(error);
                }

                return 3;
            }
        }

        Directory.CreateDirectory(options.Output);

        string author = AuditMetadataService.ResolveAuthor();
        string? commit = AuditMetadataService.ResolveGitCommit(context.WorkingDirectory);

        foreach (ExtractedRuleDefinition definition in definitions)
        {
            string rendered = RuleClassWriter.Render(definition, options.TenantId, author, commit);

            string outputDir = options.OrganizeByNamespace
                ? Path.Combine(options.Output, (definition.SourceNamespace ?? "global").Replace('.', Path.DirectorySeparatorChar))
                : options.Output;
            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir, $"{RuleClassWriter.ToIdentifier(definition.Code)}.g.cs");
            await File.WriteAllTextAsync(outputPath, rendered);
        }

        if (options.GenerateTests)
        {
            string testDir = Path.Combine(options.Output, "Tests");
            Directory.CreateDirectory(testDir);
            DiscoveredRuleType[] ruleTypes = [.. definitions
                .Select(d => new DiscoveredRuleType($"{RuleClassWriter.ToIdentifier(d.Code)}Rule", d.ContextType))
                .DistinctBy(x => $"{x.ClassName}|{x.ContextType}")];

            foreach (DiscoveredRuleType? ruleType in ruleTypes)
            {
                string testContent = TestScaffoldWriter.Render(ruleType, $"{options.Namespace}.Tests");
                string testPath = Path.Combine(testDir, $"{ruleType.ClassName}Tests.cs");
                await File.WriteAllTextAsync(testPath, testContent);
            }
        }

        stopwatch.Stop();
        PrintSummaryTable(definitions, sourceFiles.Count, stopwatch.ElapsedMilliseconds);

        // ── Auto-register: generate registration extension + dispatchers ──
        if (options.AutoRegister)
        {
            await RunAutoRegisterAsync(options, definitions);
        }

        // ── Proliferate: generate scenarios + inline coverage report ──
        if (options.Proliferate)
        {
            if (string.IsNullOrWhiteSpace(options.WorkflowName))
            {
                CommandLineWriter.WriteWarning("--proliferate requires --workflow-name to be specified. Skipping proliferation.");
            }
            else
            {
                // Build a synthetic ruleset JSON from extracted definitions for the named workflow
                string ruleSetJson = BuildSyntheticRuleSetJson(options.WorkflowName, definitions);
                try
                {
                    await ProliferateCommand.RunInlineAsync(options.WorkflowName, ruleSetJson, context.WorkingDirectory);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] Proliferation failed: {ex.Message}");
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Builds a minimal ruleset JSON from extracted rule definitions so that the
    /// proliferation engine can analyze the workflow's input fields.
    /// </summary>
    private static string BuildSyntheticRuleSetJson(string workflowName, IReadOnlyList<ExtractedRuleDefinition> definitions)
    {
        // Collect distinct parameter names across all definitions as input fields
        var inputFields = definitions
            .SelectMany(d => d.Parameters.Select(p => new { name = p.Name, type = p.TypeName ?? "string" }))
            .DistinctBy(p => p.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build a simple JSON structure that RuleSetSchemaExtractor can parse
        var fieldsJson = string.Join(",\n    ", inputFields.Select(f =>
            $"{{\"name\":\"{f.name}\",\"dataType\":\"{f.type}\",\"isRequired\":true}}"));

        return $$"""
            {
              "workflowName": "{{workflowName}}",
              "kind": "CodeBased",
              "inputFields": [
                {{fieldsJson}}
              ],
              "outputFields": []
            }
            """;
    }

    private static async Task RunAutoRegisterAsync(ExtractOptions options, IReadOnlyList<ExtractedRuleDefinition> definitions)
    {
        string registrationNamespace = options.RegistrationNamespace ?? options.Namespace;
        string dispatcherNamespace = options.DispatcherNamespace ?? registrationNamespace;
        string dispatcherDir = options.DispatcherOutput ?? options.Output;

        DiscoveredRuleType[] discovered = [.. definitions
            .Select(d => new DiscoveredRuleType($"{RuleClassWriter.ToIdentifier(d.Code)}Rule", d.ContextType))
            .DistinctBy(x => $"{x.ClassName}|{x.ContextType}")];

        IReadOnlyList<DiscoveredDispatcherContext> dispatchers = DispatcherWriter.BuildContexts(
            discovered,
            options.DispatcherSuffix,
            options.WorkflowName);

        IReadOnlyList<DiscoveredDispatcherContext> dispatchersForRegistration =
            options.RegisterDispatchers ? dispatchers : [];

        string rendered = RegistrationWriter.Render(
            registrationNamespace,
            discovered,
            dispatchersForRegistration,
            options.IncludeRuleEngine,
            options.RegistrationClassName);

        string registrationFile = Path.Combine(options.Output, options.RegistrationFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(registrationFile)!);
        await File.WriteAllTextAsync(registrationFile, rendered);

        int generatedDispatcherCount = 0;
        int skippedDispatcherCount = 0;
        if (options.GenerateDispatchers)
        {
            Directory.CreateDirectory(dispatcherDir);
            foreach (DiscoveredDispatcherContext dispatcher in dispatchers)
            {
                string dispatcherFile = Path.Combine(dispatcherDir, dispatcher.FileName);
                if (File.Exists(dispatcherFile) && !options.DispatcherOverwrite)
                {
                    skippedDispatcherCount++;
                    continue;
                }

                string content = DispatcherWriter.Render(dispatcherNamespace, dispatcher);
                await File.WriteAllTextAsync(dispatcherFile, content);
                generatedDispatcherCount++;
            }
        }

        AnsiConsole.MarkupLine($"[green]Auto-register:[/] registration '{Path.GetFileName(registrationFile)}' ({discovered.Length} rules).");
        if (options.GenerateDispatchers)
        {
            AnsiConsole.MarkupLine(
                $"[green]Dispatchers:[/] generated {generatedDispatcherCount}, skipped {skippedDispatcherCount}, output: '{dispatcherDir}'.");
        }
    }

    private static void PrintSummaryTable(IReadOnlyList<ExtractedRuleDefinition> definitions, int fileCount, long elapsedMs)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("File");
        table.AddColumn("Rules");
        table.AddColumn("Status");

        var grouped = definitions.GroupBy(d => Path.GetFileName(d.SourceFile));
        foreach (var group in grouped)
        {
            table.AddRow(group.Key, group.Count().ToString(), "[green]✓ OK[/]");
        }

        AnsiConsole.Write(new Panel(table).Header("[bold green]Rule Extraction Summary[/]"));
        AnsiConsole.MarkupLine($"Total: [bold yellow]{definitions.Count}[/] rules from [bold yellow]{fileCount}[/] files in [bold blue]{elapsedMs}[/] ms.");
    }

    internal sealed class ExtractOptions
    {
        private static readonly Regex FileScopedNamespaceRegex =
            new(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        private static readonly Regex BlockNamespaceRegex =
            new(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*\{", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public required string Output { get; init; }
        public required string Namespace { get; init; }
        public required string Pattern { get; init; }
        public required IReadOnlyList<string> ExcludePatterns { get; init; }
        public string? Source { get; init; }
        public string? SourceDir { get; init; }
        public string? Project { get; init; }
        public string? ContextType { get; init; }
        public string? TenantId { get; init; }
        public bool GenerateTests { get; init; }
        public bool Validate { get; init; }
        public bool OrganizeByNamespace { get; init; }
        public bool Parallel { get; init; }

        // ── Auto-register options ──
        public bool AutoRegister { get; init; }
        public string RegistrationFileName { get; init; } = "MGeneratedRuleRegistrationExtensions.g.cs";
        public string RegistrationClassName { get; init; } = "MGeneratedRuleRegistrationExtensions";
        public string? RegistrationNamespace { get; init; }
        public bool GenerateDispatchers { get; init; }
        public bool RegisterDispatchers { get; init; }
        public bool IncludeRuleEngine { get; init; }
        public string? DispatcherOutput { get; init; }
        public string? DispatcherNamespace { get; init; }
        public bool DispatcherOverwrite { get; init; }
        public string DispatcherSuffix { get; init; } = "GeneratedRuleEngineDispatcher";
        public string? WorkflowName { get; init; }

        // ── Proliferate flag (CLI-only, no config fallback) ──
        public bool Proliferate { get; init; }

        public static ExtractOptions FromContext(CommandContext context)
        {
            RuleGenExtractConfig cfg = context.Config.Extract;

            string? source = OptionReader.GetString(context, "source", cfg.Source);
            string? sourceDir = OptionReader.GetString(context, "source-dir", cfg.SourceDir);
            string? project = OptionReader.GetString(context, "project", cfg.Project);
            string? output = OptionReader.GetString(context, "output", cfg.OutputDir);
            string resolvedOutput = ResolveOutputPath(context, output, source, sourceDir, project);
            string? namespaceOption = OptionReader.GetString(context, "namespace");
            string resolvedNamespace = ResolveNamespace(
                context,
                namespaceOption,
                cfg.Namespace,
                source,
                sourceDir,
                project,
                resolvedOutput);

            bool autoRegister = OptionReader.GetBool(context, "auto-register", cfg.AutoRegister);
            bool generateDispatchers = OptionReader.GetBool(context, "generate-dispatchers", cfg.GenerateDispatchers);

            return new ExtractOptions
            {
                Source = source,
                SourceDir = sourceDir,
                Project = project,
                Output = resolvedOutput,
                Namespace = resolvedNamespace,
                ContextType = OptionReader.GetString(context, "context", cfg.ContextType),
                Pattern = OptionReader.GetString(context, "pattern", cfg.Pattern) ?? "**/*.cs",
                ExcludePatterns = OptionReader.GetCsvList(context, "exclude", cfg.ExcludePatterns),
                GenerateTests = OptionReader.GetBool(context, "generate-tests", cfg.GenerateTests),
                Validate = OptionReader.GetBool(context, "validate", cfg.Validate),
                OrganizeByNamespace = OptionReader.GetBool(context, "organize-by-namespace", cfg.OrganizeByNamespace),
                Parallel = OptionReader.GetBool(context, "parallel", cfg.Parallel),
                TenantId = OptionReader.GetString(context, "tenant"),
                // Auto-register options
                AutoRegister = autoRegister,
                RegistrationFileName = OptionReader.GetString(context, "registration-file-name", cfg.RegistrationFileName)
                    ?? "MGeneratedRuleRegistrationExtensions.g.cs",
                RegistrationClassName = OptionReader.GetString(context, "registration-class", cfg.RegistrationClassName)
                    ?? "MGeneratedRuleRegistrationExtensions",
                RegistrationNamespace = OptionReader.GetString(context, "registration-namespace", cfg.RegistrationNamespace),
                GenerateDispatchers = generateDispatchers,
                RegisterDispatchers = OptionReader.GetBool(context, "register-dispatchers", cfg.RegisterDispatchers),
                IncludeRuleEngine = OptionReader.GetBool(context, "include-rule-engine", cfg.IncludeRuleEngine),
                DispatcherOutput = OptionReader.GetString(context, "dispatcher-output", cfg.DispatcherOutput),
                DispatcherNamespace = OptionReader.GetString(context, "dispatcher-namespace", cfg.DispatcherNamespace),
                DispatcherOverwrite = OptionReader.GetBool(context, "dispatcher-overwrite", cfg.DispatcherOverwrite),
                DispatcherSuffix = OptionReader.GetString(context, "dispatcher-suffix", cfg.DispatcherSuffix)
                    ?? "GeneratedRuleEngineDispatcher",
                WorkflowName = OptionReader.GetString(context, "workflow-name", cfg.WorkflowName),
                // CLI-only flag: no config fallback
                Proliferate = OptionReader.GetBool(context, "proliferate", false)
            };
        }

        private static string ResolveOutputPath(
            CommandContext context,
            string? output,
            string? source,
            string? sourceDir,
            string? project)
        {
            if (!string.IsNullOrWhiteSpace(output))
            {
                return Path.GetFullPath(output, context.WorkingDirectory);
            }

            if (!string.IsNullOrWhiteSpace(source))
            {
                string sourcePath = Path.GetFullPath(source, context.WorkingDirectory);
                if (Directory.Exists(sourcePath))
                {
                    return Path.Combine(sourcePath, "Rules");
                }

                string sourceParent = Path.GetDirectoryName(sourcePath)
                    ?? throw new MInternalException($"Cannot resolve parent directory for source path '{sourcePath}'.");
                return Path.Combine(sourceParent, "Rules");
            }

            if (!string.IsNullOrWhiteSpace(sourceDir))
            {
                string sourceDirPath = Path.GetFullPath(sourceDir, context.WorkingDirectory);
                return Path.Combine(sourceDirPath, "Rules");
            }

            if (!string.IsNullOrWhiteSpace(project))
            {
                string projectPath = Path.GetFullPath(project, context.WorkingDirectory);
                string projectDir = Path.GetDirectoryName(projectPath)
                    ?? throw new MInternalException($"Cannot resolve directory for project path '{projectPath}'.");
                return Path.Combine(projectDir, "Generated", "Rules");
            }

            throw new MInternalException(
                "Missing output location. Provide --output or at least one of --source/--source-dir/--project so output can be inferred.");
        }

        private static string ResolveNamespace(
            CommandContext context,
            string? namespaceOption,
            string? configuredNamespace,
            string? source,
            string? sourceDir,
            string? project,
            string resolvedOutputPath)
        {
            if (!string.IsNullOrWhiteSpace(namespaceOption))
            {
                return namespaceOption!;
            }

            string? sourceNamespace = TryResolveSourceNamespace(context, source, sourceDir, project);
            if (string.IsNullOrWhiteSpace(sourceNamespace))
            {
                return configuredNamespace ?? "Generated.Rules";
            }

            string sourceAnchorDirectory = ResolveSourceAnchorDirectory(context, source, sourceDir, project);
            string computed = ComputeNamespaceByRelativePath(sourceNamespace!, sourceAnchorDirectory, resolvedOutputPath);
            if (!string.IsNullOrWhiteSpace(computed))
            {
                return computed;
            }

            return configuredNamespace ?? sourceNamespace!;
        }

        private static string ResolveSourceAnchorDirectory(
            CommandContext context,
            string? source,
            string? sourceDir,
            string? project)
        {
            if (!string.IsNullOrWhiteSpace(source))
            {
                string sourcePath = Path.GetFullPath(source, context.WorkingDirectory);
                if (Directory.Exists(sourcePath))
                {
                    return sourcePath;
                }

                return Path.GetDirectoryName(sourcePath) ?? context.WorkingDirectory;
            }

            if (!string.IsNullOrWhiteSpace(sourceDir))
            {
                return Path.GetFullPath(sourceDir, context.WorkingDirectory);
            }

            if (!string.IsNullOrWhiteSpace(project))
            {
                string projectPath = Path.GetFullPath(project, context.WorkingDirectory);
                return Path.GetDirectoryName(projectPath) ?? context.WorkingDirectory;
            }

            return context.WorkingDirectory;
        }

        private static string? TryResolveSourceNamespace(
            CommandContext context,
            string? source,
            string? sourceDir,
            string? project)
        {
            if (!string.IsNullOrWhiteSpace(source))
            {
                string sourcePath = Path.GetFullPath(source, context.WorkingDirectory);
                if (File.Exists(sourcePath) && sourcePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    string sourceContent = File.ReadAllText(sourcePath);
                    string ns = TryReadNamespace(sourceContent);
                    if (!string.IsNullOrWhiteSpace(ns))
                    {
                        return ns;
                    }
                }

                if (Directory.Exists(sourcePath))
                {
                    return TryReadNamespaceFromDirectory(sourcePath);
                }
            }

            if (!string.IsNullOrWhiteSpace(sourceDir))
            {
                string sourceDirectory = Path.GetFullPath(sourceDir, context.WorkingDirectory);
                if (Directory.Exists(sourceDirectory))
                {
                    return TryReadNamespaceFromDirectory(sourceDirectory);
                }
            }

            if (!string.IsNullOrWhiteSpace(project))
            {
                string projectPath = Path.GetFullPath(project, context.WorkingDirectory);
                string projectDirectory = Path.GetDirectoryName(projectPath) ?? context.WorkingDirectory;
                if (Directory.Exists(projectDirectory))
                {
                    return TryReadNamespaceFromDirectory(projectDirectory);
                }
            }

            return null;
        }

        private static string? TryReadNamespaceFromDirectory(string directoryPath)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories))
                {
                    string content = File.ReadAllText(file);
                    string ns = TryReadNamespace(content);
                    if (!string.IsNullOrWhiteSpace(ns))
                    {
                        return ns;
                    }
                }
            }
            catch
            {
                // best effort only
            }

            return null;
        }

        private static string TryReadNamespace(string content)
        {
            Match m1 = FileScopedNamespaceRegex.Match(content ?? string.Empty);
            if (m1.Success)
            {
                return m1.Groups[1].Value.Trim();
            }

            Match m2 = BlockNamespaceRegex.Match(content ?? string.Empty);
            return m2.Success ? m2.Groups[1].Value.Trim() : string.Empty;
        }

        private static string ComputeNamespaceByRelativePath(
            string sourceNamespace,
            string sourceAnchorDirectory,
            string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceNamespace))
            {
                return string.Empty;
            }

            List<string> namespaceParts = [.. sourceNamespace.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)];
            string sourceFullPath = Path.GetFullPath(sourceAnchorDirectory);
            string targetFullPath = Path.GetFullPath(targetDirectory);
            string relative = Path.GetRelativePath(sourceFullPath, targetFullPath);
            string[] segments = relative
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in segments)
            {
                string segment = raw.Trim();
                if (string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    if (namespaceParts.Count > 0)
                    {
                        namespaceParts.RemoveAt(namespaceParts.Count - 1);
                    }

                    continue;
                }

                string normalized = NormalizeNamespaceSegment(segment);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    namespaceParts.Add(normalized);
                }
            }

            return namespaceParts.Count == 0 ? sourceNamespace : string.Join(".", namespaceParts);
        }

        private static string NormalizeNamespaceSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            char[] buffer = segment.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!char.IsLetterOrDigit(buffer[i]) && buffer[i] != '_')
                {
                    buffer[i] = '_';
                }
            }

            string normalized = new string(buffer).Trim('_');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (char.IsDigit(normalized[0]))
            {
                normalized = "_" + normalized;
            }

            return normalized;
        }
    }
}
