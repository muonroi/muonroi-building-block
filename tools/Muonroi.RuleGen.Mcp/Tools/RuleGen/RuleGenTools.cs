namespace Muonroi.RuleGen.Mcp.Tools.RuleGen;

internal static class RuleGenToolHelpers
{
    public static string ResolveWorkingDirectory(string? workingDirectory)
    {
        return string.IsNullOrWhiteSpace(workingDirectory)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(workingDirectory);
    }

    public static CommandContext CreateContext(Dictionary<string, string> options, string? workingDirectory)
    {
        return RuleGenCommandContextFactory.Create(options, ResolveWorkingDirectory(workingDirectory));
    }

    public static async Task<RuleGenExtractResult> ExecuteExtractAsync(CommandContext context, CancellationToken ct)
    {
        ExtractCommand.ExtractOptions options = ExtractCommand.ExtractOptions.FromContext(context);
        IReadOnlyList<string> sourceFiles = SourceDiscoveryService.Discover(
            context.WorkingDirectory,
            options.Source,
            options.SourceDir,
            options.Project,
            options.Pattern,
            options.ExcludePatterns);

        IReadOnlyList<ExtractedRuleDefinition> definitions = await RoslynRuleExtractor.ExtractAsync(
            sourceFiles,
            options.Namespace,
            options.ContextType,
            options.Parallel,
            ct);

        List<string> warnings = [];
        List<string> errors = [];
        if (options.Validate)
        {
            ValidationReport report = RuleValidationService.Validate(definitions);
            warnings.AddRange(report.Warnings);
            errors.AddRange(report.Errors);
        }

        if (errors.Count > 0)
        {
            return new RuleGenExtractResult(0, [], warnings, errors);
        }

        Directory.CreateDirectory(options.Output);
        string author = AuditMetadataService.ResolveAuthor();
        string? commit = AuditMetadataService.ResolveGitCommit(context.WorkingDirectory);
        List<string> files = [];

        foreach (ExtractedRuleDefinition definition in definitions)
        {
            string rendered = RuleClassWriter.Render(definition, options.TenantId, author, commit);
            string outputDir = options.OrganizeByNamespace
                ? Path.Combine(options.Output, (definition.SourceNamespace ?? "global").Replace('.', Path.DirectorySeparatorChar))
                : options.Output;
            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir, $"{RuleClassWriter.ToIdentifier(definition.Code)}.g.cs");
            await File.WriteAllTextAsync(outputPath, rendered, ct);
            files.Add(outputPath);
        }

        if (options.GenerateTests)
        {
            string testDir = Path.Combine(options.Output, "Tests");
            Directory.CreateDirectory(testDir);
            DiscoveredRuleType[] ruleTypes = [.. definitions
                .Select(d => new DiscoveredRuleType($"{RuleClassWriter.ToIdentifier(d.Code)}Rule", d.ContextType))
                .DistinctBy(x => $"{x.ClassName}|{x.ContextType}")];

            foreach (DiscoveredRuleType ruleType in ruleTypes)
            {
                string testContent = TestScaffoldWriter.Render(ruleType, $"{options.Namespace}.Tests");
                string testPath = Path.Combine(testDir, $"{ruleType.ClassName}Tests.cs");
                await File.WriteAllTextAsync(testPath, testContent, ct);
                files.Add(testPath);
            }
        }

        return new RuleGenExtractResult(definitions.Count, files, warnings, errors);
    }
}

[McpServerToolType]
public sealed class ExtractRulesTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_rulegen_extract")]
    public async Task<string> ExecuteAsync(
        [Description("Source directory or file path")] string sourceDir,
        [Description("Output directory for generated .g.cs files")] string outputDir,
        [Description("Optional namespace override")] string? @namespace = null,
        [Description("Optional context type override")] string? contextType = null,
        [Description("Generate test scaffolds too")] bool generateTests = false,
        [Description("Validate extracted rules before writing")] bool validate = true,
        [Description("Optional tenant id metadata")] string? tenantId = null,
        [Description("Glob pattern for source files")] string pattern = "**/*.cs",
        [Description("Parallel extraction")] bool parallel = false,
        [Description("Optional working directory")] string? workingDirectory = null,
        CancellationToken ct = default)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = sourceDir,
            ["output"] = outputDir,
            ["pattern"] = pattern,
            ["generate-tests"] = generateTests.ToString(),
            ["validate"] = validate.ToString(),
            ["parallel"] = parallel.ToString()
        };

        if (!string.IsNullOrWhiteSpace(@namespace))
        {
            options["namespace"] = @namespace;
        }

        if (!string.IsNullOrWhiteSpace(contextType))
        {
            options["context"] = contextType;
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            options["tenant"] = tenantId;
        }

        RuleGenExtractResult result = await RuleGenToolHelpers.ExecuteExtractAsync(RuleGenToolHelpers.CreateContext(options, workingDirectory), ct);
        return jsonService.Serialize(result);
    }
}

[McpServerToolType]
public sealed class VerifyRulesTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_rulegen_verify")]
    public async Task<string> ExecuteAsync(
        string sourceDir,
        string rulesDir,
        string? @namespace = null,
        string? contextType = null,
        string pattern = "**/*.cs",
        bool parallel = false,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = sourceDir,
            ["rules"] = rulesDir,
            ["pattern"] = pattern,
            ["parallel"] = parallel.ToString()
        };

        if (!string.IsNullOrWhiteSpace(@namespace))
        {
            options["namespace"] = @namespace;
        }

        if (!string.IsNullOrWhiteSpace(contextType))
        {
            options["context"] = contextType;
        }

        CommandContext context = RuleGenToolHelpers.CreateContext(options, workingDirectory);
        string rulesPath = Path.GetFullPath(rulesDir, context.WorkingDirectory);
        IReadOnlyList<string> files = SourceDiscoveryService.Discover(context.WorkingDirectory, sourceDir, null, null, pattern, []);
        IReadOnlyList<ExtractedRuleDefinition> definitions = await RoslynRuleExtractor.ExtractAsync(
            files,
            @namespace ?? context.Config.Extract.Namespace ?? "Generated.Rules",
            contextType,
            parallel,
            ct);

        HashSet<string> expected = definitions
            .Select(d => $"{RuleClassWriter.ToIdentifier(d.Code)}.g.cs")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> actual = Directory.GetFiles(rulesPath, "*.g.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RuleGenVerifyResult result = new(
            expected.SetEquals(actual),
            expected.Except(actual, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            actual.Except(expected, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());

        return jsonService.Serialize(result);
    }
}

[McpServerToolType]
public sealed class RegisterRulesTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_rulegen_register")]
    public async Task<string> ExecuteAsync(
        string rulesDir,
        string outputFile,
        string? @namespace = null,
        string registrationClass = "MGeneratedRuleRegistrationExtensions",
        bool generateDispatchers = true,
        string? dispatcherOutputDir = null,
        string? dispatcherNamespace = null,
        bool registerDispatchers = true,
        bool includeRuleEngineRegistration = true,
        string dispatcherSuffix = "GeneratedRuleEngineDispatcher",
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        string cwd = RuleGenToolHelpers.ResolveWorkingDirectory(workingDirectory);
        string resolvedRulesDir = Path.GetFullPath(rulesDir, cwd);
        string resolvedOutputFile = Path.GetFullPath(outputFile, cwd);
        string resolvedDispatcherDir = Path.GetFullPath(dispatcherOutputDir ?? Path.GetDirectoryName(resolvedOutputFile)!, cwd);
        string resolvedNamespace = @namespace ?? "Generated.Rules";
        string resolvedDispatcherNamespace = dispatcherNamespace ?? resolvedNamespace;

        IReadOnlyList<DiscoveredRuleType> discovered = RuleTypeDiscoveryService.Discover(resolvedRulesDir);
        IReadOnlyList<DiscoveredDispatcherContext> dispatchers = DispatcherWriter.BuildContexts(discovered, dispatcherSuffix);
        IReadOnlyList<DiscoveredDispatcherContext> dispatchersForRegistration = registerDispatchers ? dispatchers : [];
        string registration = RegistrationWriter.Render(
            resolvedNamespace,
            discovered,
            dispatchersForRegistration,
            includeRuleEngineRegistration,
            registrationClass);

        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutputFile)!);
        await File.WriteAllTextAsync(resolvedOutputFile, registration, ct);

        List<string> dispatcherFiles = [];
        if (generateDispatchers)
        {
            Directory.CreateDirectory(resolvedDispatcherDir);
            foreach (DiscoveredDispatcherContext dispatcher in dispatchers)
            {
                string path = Path.Combine(resolvedDispatcherDir, dispatcher.FileName);
                string content = DispatcherWriter.Render(resolvedDispatcherNamespace, dispatcher);
                await File.WriteAllTextAsync(path, content, ct);
                dispatcherFiles.Add(path);
            }
        }

        RuleGenRegisterResult result = new(resolvedOutputFile, dispatcherFiles.Count, discovered.Count, dispatcherFiles);
        return jsonService.Serialize(result);
    }
}

[McpServerToolType]
public sealed class GenerateTestsTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_rulegen_generate_tests")]
    public async Task<string> ExecuteAsync(
        string rulesDir,
        string outputDir,
        string @namespace = "Generated.Rules",
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        string cwd = RuleGenToolHelpers.ResolveWorkingDirectory(workingDirectory);
        string resolvedRulesDir = Path.GetFullPath(rulesDir, cwd);
        string resolvedOutputDir = Path.GetFullPath(outputDir, cwd);

        Directory.CreateDirectory(resolvedOutputDir);
        IReadOnlyList<DiscoveredRuleType> discovered = RuleTypeDiscoveryService.Discover(resolvedRulesDir);
        List<string> files = [];
        foreach (DiscoveredRuleType rule in discovered)
        {
            string content = TestScaffoldWriter.Render(rule, $"{@namespace}.Tests");
            string path = Path.Combine(resolvedOutputDir, $"{rule.ClassName}Tests.cs");
            await File.WriteAllTextAsync(path, content, ct);
            files.Add(path);
        }

        return jsonService.Serialize(new RuleGenGenerateTestsResult(files.Count, files));
    }
}

[McpServerToolType]
public sealed class MergeRulesTool(
    IMJsonSerializeService jsonService,
    ConsoleIsolationRunner consoleIsolationRunner)
{
    [McpServerTool(Name = "muonroi_rulegen_merge")]
    public async Task<string> ExecuteAsync(
        string target,
        string? rulesJson = null,
        string? rulesDir = null,
        string? sourceDir = null,
        string? classTarget = null,
        string? contextType = null,
        string strategy = "append",
        bool partialClass = true,
        bool compileCheck = true,
        string? compileTarget = null,
        string? workflowName = null,
        string? tenantId = null,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase)
        {
            ["target"] = target,
            ["strategy"] = strategy,
            ["partial-class"] = partialClass.ToString(),
            ["compile-check"] = compileCheck.ToString()
        };

        if (!string.IsNullOrWhiteSpace(rulesJson)) options["rules-json"] = rulesJson;
        if (!string.IsNullOrWhiteSpace(rulesDir)) options["rules-dir"] = rulesDir;
        if (!string.IsNullOrWhiteSpace(sourceDir)) options["source-dir"] = sourceDir;
        if (!string.IsNullOrWhiteSpace(classTarget)) options["class"] = classTarget;
        if (!string.IsNullOrWhiteSpace(contextType)) options["context"] = contextType;
        if (!string.IsNullOrWhiteSpace(compileTarget)) options["compile-target"] = compileTarget;
        if (!string.IsNullOrWhiteSpace(workflowName)) options["workflow"] = workflowName;
        if (!string.IsNullOrWhiteSpace(tenantId)) options["tenant"] = tenantId;

        CommandContext context = RuleGenToolHelpers.CreateContext(options, workingDirectory);
        int exitCode = await consoleIsolationRunner.RunAsync(() => MergeCommand.RunAsync(context), ct);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"RuleGen merge failed with exit code {exitCode}.");
        }

        List<string> outputFiles = [];
        string resolvedTarget = Path.GetFullPath(target, context.WorkingDirectory);
        outputFiles.Add(resolvedTarget);

        if (!string.IsNullOrWhiteSpace(rulesJson) && File.Exists(Path.GetFullPath(rulesJson, context.WorkingDirectory)))
        {
            RuntimeRuleSet rules = RuntimeRuleJsonService.Load(Path.GetFullPath(rulesJson, context.WorkingDirectory), workflowName, tenantId);
            string generatedDir = Path.GetDirectoryName(resolvedTarget)!;
            string generatedClassName = Path.GetFileNameWithoutExtension(resolvedTarget);
            string generatedFile = Path.Combine(generatedDir, $"{generatedClassName}.Generated.cs");
            if (File.Exists(generatedFile))
            {
                outputFiles.Add(generatedFile);
            }

            return jsonService.Serialize(new RuleGenMergeResult(rules.Rules.Count, resolvedTarget, outputFiles));
        }

        return jsonService.Serialize(new RuleGenMergeResult(outputFiles.Count, resolvedTarget, outputFiles));
    }
}

[McpServerToolType]
public sealed class SplitRulesTool(
    IMJsonSerializeService jsonService,
    ConsoleIsolationRunner consoleIsolationRunner)
{
    [McpServerTool(Name = "muonroi_rulegen_split")]
    public async Task<string> ExecuteAsync(
        string source,
        string outputDir,
        string? exportJson = null,
        string? classTarget = null,
        string? workflowName = null,
        string? contextType = null,
        string? tenantId = null,
        int version = 1,
        string pattern = "**/*.cs",
        bool parallel = false,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = source,
            ["output-dir"] = outputDir,
            ["pattern"] = pattern,
            ["parallel"] = parallel.ToString(),
            ["version"] = version.ToString()
        };

        if (!string.IsNullOrWhiteSpace(exportJson)) options["export-json"] = exportJson;
        if (!string.IsNullOrWhiteSpace(classTarget)) options["class"] = classTarget;
        if (!string.IsNullOrWhiteSpace(workflowName)) options["workflow"] = workflowName;
        if (!string.IsNullOrWhiteSpace(contextType)) options["context"] = contextType;
        if (!string.IsNullOrWhiteSpace(tenantId)) options["tenant"] = tenantId;

        CommandContext context = RuleGenToolHelpers.CreateContext(options, workingDirectory);
        int exitCode = await consoleIsolationRunner.RunAsync(() => SplitCommand.RunAsync(context), ct);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"RuleGen split failed with exit code {exitCode}.");
        }

        string resolvedOutputDir = Path.GetFullPath(outputDir, context.WorkingDirectory);
        IReadOnlyList<string> files = Directory.GetFiles(resolvedOutputDir, "*.g.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return jsonService.Serialize(new RuleGenSplitResult(files.Count, files, string.IsNullOrWhiteSpace(exportJson) ? null : Path.GetFullPath(exportJson, context.WorkingDirectory)));
    }
}

[McpServerToolType]
public sealed class WatchRulesTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_rulegen_watch")]
    public async Task<string> ExecuteAsync(
        string sourceDir,
        string outputDir,
        string? @namespace = null,
        string? contextType = null,
        int debounceMs = 600,
        bool runInitialExtract = true,
        int durationSeconds = 10,
        string pattern = "**/*.cs",
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = sourceDir,
            ["output"] = outputDir,
            ["pattern"] = pattern
        };
        if (!string.IsNullOrWhiteSpace(@namespace)) options["namespace"] = @namespace;
        if (!string.IsNullOrWhiteSpace(contextType)) options["context"] = contextType;

        CommandContext context = RuleGenToolHelpers.CreateContext(options, workingDirectory);
        string resolvedWatchDir = Path.GetFullPath(sourceDir, context.WorkingDirectory);
        int eventCount = 0;
        DateTime? lastRunAtUtc = null;
        RuleGenExtractResult lastExtract = new(0, [], [], []);

        using FileSystemWatcher watcher = new(resolvedWatchDir, "*.cs") { IncludeSubdirectories = true, EnableRaisingEvents = true };
        using CancellationTokenSource durationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        durationCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, durationSeconds)));
        SemaphoreSlim gate = new(1, 1);
        Timer? timer = null;

        async Task RunExtractAsync()
        {
            await gate.WaitAsync(durationCts.Token);
            try
            {
                lastExtract = await RuleGenToolHelpers.ExecuteExtractAsync(context, durationCts.Token);
                lastRunAtUtc = DateTime.UtcNow; // MBB001-exempt: watcher bookkeeping inside tooling server
            }
            finally
            {
                gate.Release();
            }
        }

        void Trigger()
        {
            Interlocked.Increment(ref eventCount);
            lock (gate)
            {
                timer?.Dispose();
                timer = new Timer(async _ =>
                {
                    try
                    {
                        await RunExtractAsync();
                    }
                    catch
                    {
                        // surfaced in lastExtract
                    }
                }, null, debounceMs, Timeout.Infinite);
            }
        }

        watcher.Changed += (_, _) => Trigger();
        watcher.Created += (_, _) => Trigger();
        watcher.Deleted += (_, _) => Trigger();
        watcher.Renamed += (_, _) => Trigger();

        if (runInitialExtract)
        {
            await RunExtractAsync();
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, durationCts.Token);
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            lock (gate)
            {
                timer?.Dispose();
            }
        }

        RuleGenWatchResult result = new(true, lastExtract.Errors.Count == 0, eventCount, lastRunAtUtc, lastExtract.Files);
        return jsonService.Serialize(result);
    }
}

[McpServerToolType]
public sealed class TranslateFeelTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_rulegen_translate_feel")]
    public string Execute(
        string expression,
        [Description("feel-to-csharp or csharp-to-feel")] string direction)
    {
        string translated = string.Equals(direction, "csharp-to-feel", StringComparison.OrdinalIgnoreCase)
            ? FeelCSharpTranslator.CSharpToFeel(expression)
            : FeelCSharpTranslator.FeelToCSharpCondition(expression);

        return jsonService.Serialize(new { translated, direction });
    }
}

[McpServerToolType]
public sealed class LoadRulesetJsonTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_rulegen_load_ruleset_json")]
    public string Execute(string path, string? defaultWorkflow = null, string? tenantId = null, string? workingDirectory = null)
    {
        string cwd = RuleGenToolHelpers.ResolveWorkingDirectory(workingDirectory);
        RuntimeRuleSet ruleSet = RuntimeRuleJsonService.Load(Path.GetFullPath(path, cwd), defaultWorkflow, tenantId);
        return jsonService.Serialize(ruleSet);
    }
}
