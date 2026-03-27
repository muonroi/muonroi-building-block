using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Services;
using Muonroi.RuleGen.Writers;
using Spectre.Console;
using System.Text;

namespace Muonroi.RuleGen.Commands;

internal static class MergeCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        string? rulesJson = OptionReader.GetString(context, "rules-json");
        string? rulesDir = OptionReader.GetString(context, "rules-dir");
        string? sourceDir = OptionReader.GetString(context, "source-dir");
        string? target = OptionReader.GetString(context, "target");

        int specifiedModes = 0;
        if (!string.IsNullOrWhiteSpace(rulesJson))
        {
            specifiedModes++;
        }

        if (!string.IsNullOrWhiteSpace(rulesDir))
        {
            specifiedModes++;
        }

        if (!string.IsNullOrWhiteSpace(sourceDir))
        {
            specifiedModes++;
        }

        if (specifiedModes == 0)
        {
            throw new InvalidOperationException("Missing required option --rules-json or --rules-dir or --source-dir.");
        }

        if (specifiedModes > 1)
        {
            throw new InvalidOperationException("Use only one of --rules-json | --rules-dir | --source-dir.");
        }

        if (!string.IsNullOrWhiteSpace(rulesDir))
        {
            string resolvedTarget = ResolveTargetForRulesDir(context, rulesDir, target);
            return await RunMergeFromGeneratedRulesAsync(context, rulesDir, resolvedTarget);
        }

        if (!string.IsNullOrWhiteSpace(sourceDir))
        {
            string resolvedTarget = ResolveTargetForSourceDir(context, sourceDir, target);
            return await RunMergeFromAttributeSourcesAsync(context, sourceDir, resolvedTarget);
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Missing required option --target.");
        }

        string strategy = (OptionReader.GetString(context, "strategy") ?? "append").ToLowerInvariant();
        bool partialClass = OptionReader.GetBool(context, "partial-class", context.Config.Conventions.RequirePartialForMerge);
        string? classTarget = OptionReader.GetString(context, "class");

        bool compileCheckEnabled = OptionReader.GetBool(context, "compile-check", true);
        string? compileTargetOption = OptionReader.GetString(context, "compile-target");

        string rulesPath = Path.GetFullPath(rulesJson!, context.WorkingDirectory);
        string targetPath = Path.GetFullPath(target, context.WorkingDirectory);
        
        RuntimeRuleSet runtimeRules = RuntimeRuleJsonService.Load(
            rulesPath,
            OptionReader.GetString(context, "workflow"),
            OptionReader.GetString(context, "tenant"));

        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException($"Target file not found: {targetPath}");
        }

        string originalTargetContent = await File.ReadAllTextAsync(targetPath);
        string? generatedFile = null;
        string? originalGeneratedContent = null;
        bool generatedExisted = false;

        return await AnsiConsole.Status()
            .StartAsync("[yellow]Merging rules...[/]", async ctx =>
            {
                try
                {
                    (string targetNamespace, string className, string contextType) = await EnsurePartialAndReadTargetAsync(
                        targetPath,
                        partialClass,
                        OptionReader.GetString(context, "context"),
                        classTarget);

                    generatedFile = Path.Combine(Path.GetDirectoryName(targetPath)!, $"{className}.Generated.cs");
                    generatedExisted = File.Exists(generatedFile);
                    if (generatedExisted)
                    {
                        originalGeneratedContent = await File.ReadAllTextAsync(generatedFile);
                    }

                    Dictionary<string, string> existing = generatedExisted
                        ? ReadExistingGeneratedMethods(generatedFile, className)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    Dictionary<string, string> incoming = new(StringComparer.OrdinalIgnoreCase);
                    foreach (RuntimeRuleDefinition? rule in runtimeRules.Rules.OrderBy(x => x.Order))
                    {
                        incoming[rule.Code] = RenderRuntimeMethod(rule, contextType);
                    }

                    Dictionary<string, string> merged = MergeByStrategy(existing, incoming, strategy);
                    string fileText = RenderGeneratedPartialClass(targetNamespace, className, merged.Values);
                    await File.WriteAllTextAsync(generatedFile, fileText);

                    if (compileCheckEnabled)
                    {
                        ctx.Status("[blue]Performing compile check...[/]");
                        string compileTarget = ResolveCompileTarget(targetPath, compileTargetOption, context.WorkingDirectory);
                        CompileCheckResult result = await CompileCheckService.BuildAsync(compileTarget);
                        if (!result.Success)
                        {
                            throw new InvalidOperationException(
                                $"Compile check failed (target: {result.TargetPath}, exit code: {result.ExitCode}).{Environment.NewLine}{result.Output}");
                        }
                    }

                    CommandLineWriter.WriteSuccess($"Merged {incoming.Count} runtime rule(s) into '{generatedFile}' with strategy '{strategy}'.");
                    return 0;
                }
                catch (Exception ex)
                {
                    await File.WriteAllTextAsync(targetPath, originalTargetContent);

                    if (!string.IsNullOrWhiteSpace(generatedFile))
                    {
                        if (generatedExisted)
                        {
                            await File.WriteAllTextAsync(generatedFile!, originalGeneratedContent ?? string.Empty);
                        }
                        else if (File.Exists(generatedFile))
                        {
                            File.Delete(generatedFile);
                        }
                    }

                    CommandLineWriter.WriteError($"Merge failed: {ex.Message}");
                    return 1;
                }
            });
    }

    private static string ResolveCompileTarget(string targetPath, string? compileTargetOption, string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(compileTargetOption))
        {
            string explicitTarget = Path.GetFullPath(compileTargetOption, workingDirectory);
            if (!File.Exists(explicitTarget))
            {
                throw new FileNotFoundException($"Compile target not found: {explicitTarget}");
            }

            return explicitTarget;
        }

        string? auto = CompileCheckService.DiscoverNearestCompileTarget(Path.GetDirectoryName(targetPath)!);
        if (string.IsNullOrWhiteSpace(auto))
        {
            throw new InvalidOperationException(
                "Cannot auto-discover compile target (.sln/.csproj). Provide --compile-target explicitly or disable --compile-check.");
        }

        return auto;
    }

    private static Dictionary<string, string> MergeByStrategy(
        Dictionary<string, string> existing,
        Dictionary<string, string> incoming,
        string strategy)
    {
        Dictionary<string, string> merged = new(existing, StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> pair in incoming)
        {
            if (!merged.ContainsKey(pair.Key))
            {
                merged[pair.Key] = pair.Value;
                continue;
            }

            switch (strategy)
            {
                case "append":
                    break;
                case "replace":
                    merged[pair.Key] = pair.Value;
                    break;
                case "interactive":
                    if (AnsiConsole.Confirm($"Rule [yellow]'{pair.Key}'[/] already exists. Replace?"))
                    {
                        merged[pair.Key] = pair.Value;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported merge strategy. Use append|replace|interactive.");
            }
        }

        return merged;
    }

    private static async Task<(string Namespace, string ClassName, string ContextType)> EnsurePartialAndReadTargetAsync(
        string targetPath,
        bool ensurePartial,
        string? contextOverride,
        string? classTarget)
    {
        string source = await File.ReadAllTextAsync(targetPath);
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

        ClassDeclarationSyntax[] candidateClasses = [.. root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => !c.Ancestors().OfType<ClassDeclarationSyntax>().Any())];

        if (candidateClasses.Length == 0)
        {
            throw new InvalidDataException($"No class declaration found in target file: {targetPath}");
        }

        ClassDeclarationSyntax selectedClass = SelectTargetClass(candidateClasses, classTarget);

        if (ensurePartial && !selectedClass.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.PartialKeyword))
        {
            ClassDeclarationSyntax partialClass = selectedClass.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
            root = root.ReplaceNode(selectedClass, partialClass);
            await File.WriteAllTextAsync(targetPath, root.NormalizeWhitespace().ToFullString());
            selectedClass = partialClass;
        }

        string ns = selectedClass.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                 ?? "Generated.Handlers";
        string className = selectedClass.Identifier.ValueText;

        string? context = contextOverride;
        if (string.IsNullOrWhiteSpace(context))
        {
            MethodDeclarationSyntax? existingRuleMethod = selectedClass.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .Any(attr => attr.Name.ToString().Contains("ExtractAsRule", StringComparison.OrdinalIgnoreCase)));

            if (existingRuleMethod is not null)
            {
                string? candidate = existingRuleMethod.ParameterList.Parameters
                    .Select(p => p.Type?.ToString())
                    .FirstOrDefault(t =>
                        !string.IsNullOrWhiteSpace(t) &&
                        !string.Equals(t, "FactBag", StringComparison.Ordinal) &&
                        !string.Equals(t, "CancellationToken", StringComparison.Ordinal));
                context = candidate;
            }
        }

        if (string.IsNullOrWhiteSpace(context))
        {
            throw new InvalidOperationException(
                $"Cannot infer context type for class '{className}'. Provide --context explicitly.");
        }

        return (ns, className, context);
    }

    private static ClassDeclarationSyntax SelectTargetClass(
        IReadOnlyList<ClassDeclarationSyntax> classes,
        string? classTarget)
    {
        if (string.IsNullOrWhiteSpace(classTarget))
        {
            if (classes.Count == 1)
            {
                return classes[0];
            }

            string available = string.Join(", ", classes.Select(GetClassDisplayName));
            throw new InvalidOperationException(
                $"Multiple classes found in target file. Provide --class. Candidates: {available}");
        }

        ClassDeclarationSyntax[] matches = [.. classes.Where(c => MatchesClassTarget(c, classTarget))];
        if (matches.Length == 0)
        {
            string available = string.Join(", ", classes.Select(GetClassDisplayName));
            throw new InvalidOperationException($"Class '{classTarget}' not found. Candidates: {available}");
        }

        if (matches.Length > 1)
        {
            string names = string.Join(", ", matches.Select(GetClassDisplayName));
            throw new InvalidOperationException($"Class target '{classTarget}' is ambiguous: {names}");
        }

        return matches[0];
    }

    private static bool MatchesClassTarget(ClassDeclarationSyntax classNode, string classTarget)
    {
        string className = classNode.Identifier.ValueText;
        string fullName = GetClassDisplayName(classNode);
        return string.Equals(className, classTarget, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fullName, classTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClassDisplayName(ClassDeclarationSyntax classNode)
    {
        string? ns = classNode.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        return string.IsNullOrWhiteSpace(ns)
            ? classNode.Identifier.ValueText
            : $"{ns}.{classNode.Identifier.ValueText}";
    }

    private static Dictionary<string, string> ReadExistingGeneratedMethods(string generatedFile, string className)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        string source = File.ReadAllText(generatedFile);
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        ClassDeclarationSyntax? classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => string.Equals(c.Identifier.ValueText, className, StringComparison.Ordinal));

        if (classNode is null)
        {
            return map;
        }

        foreach (MethodDeclarationSyntax method in classNode.Members.OfType<MethodDeclarationSyntax>())
        {
            string? code = method.AttributeLists
                .SelectMany(x => x.Attributes)
                .Where(a => a.Name.ToString().Contains("ExtractAsRule", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.ArgumentList?.Arguments.FirstOrDefault()?.Expression)
                .Where(x => x is not null)
                .Select(x => x!.ToString().Trim().Trim('"'))
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            map[code] = method.NormalizeWhitespace().ToFullString();
        }

        return map;
    }

    private static string RenderRuntimeMethod(RuntimeRuleDefinition rule, string contextType)
    {
        string methodName = RuleClassWriter.ToIdentifier(string.IsNullOrWhiteSpace(rule.Name) ? rule.Code : rule.Name);
        string condition = FeelCSharpTranslator.FeelToCSharpCondition(rule.Condition);
        string action = FeelCSharpTranslator.ActionToCSharp(rule.Action);
        string dependsOn = rule.DependsOn.Count == 0
            ? string.Empty
            : $", DependsOn = new[] {{ {string.Join(", ", rule.DependsOn.Select(d => $"\"{d}\""))} }}";

        StringBuilder sb = new();
        sb.AppendLine($"[MExtractAsRule(\"{rule.Code}\", Order = {rule.Order}, HookPoint = HookPoint.{NormalizeHookPoint(rule.HookPoint)}{dependsOn})]");
        sb.AppendLine($"public Task<RuleResult> {methodName}({contextType} ctx, FactBag facts, CancellationToken ct = default)");
        sb.AppendLine("{");
        sb.AppendLine($"    if (!({condition}))");
        sb.AppendLine("    {");
        sb.AppendLine($"        return Task.FromResult(RuleResult.Failure(\"Rule '{rule.Code}' condition not met.\"));");
        sb.AppendLine("    }");

        if (!string.IsNullOrWhiteSpace(action))
        {
            sb.AppendLine();
            sb.AppendLine($"    {action}");
        }

        sb.AppendLine();
        sb.AppendLine("    return Task.FromResult(RuleResult.Passed());");
        sb.AppendLine("}");
        return sb.ToString().Trim();
    }

    private static string RenderGeneratedPartialClass(string ns, string className, IEnumerable<string> methods)
    {
        string[] methodBlocks = [.. methods.Select(m => IndentBlock(m, 1))];

        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine($"// GeneratedAtUtc: {DateTime.UtcNow:O}");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Muonroi.RuleEngine.Abstractions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {className}");
        sb.AppendLine("{");
        foreach (string? method in methodBlocks)
        {
            sb.AppendLine(method);
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string IndentBlock(string value, int level)
    {
        string prefix = new(' ', level * 4);
        return string.Join(Environment.NewLine, value.Split(Environment.NewLine).Select(line => prefix + line));
    }

    private static async Task<int> RunMergeFromGeneratedRulesAsync(CommandContext context, string rulesDir, string target)
    {
        string rulesDirPath = Path.GetFullPath(rulesDir, context.WorkingDirectory);
        if (!Directory.Exists(rulesDirPath))
        {
            throw new DirectoryNotFoundException($"Rules directory not found: {rulesDirPath}");
        }

        string targetPath = Path.GetFullPath(target, context.WorkingDirectory);
        string searchPattern = OptionReader.GetString(context, "pattern", "*.g.cs") ?? "*.g.cs";
        bool recursive = OptionReader.GetBool(context, "recursive", true);
        string? namespaceOverride = OptionReader.GetString(context, "namespace");
        string? classOverride = OptionReader.GetString(context, "class");
        string? contextOverride = OptionReader.GetString(context, "context");
        bool compileCheckEnabled = OptionReader.GetBool(context, "compile-check", true);
        string? compileTargetOption = OptionReader.GetString(context, "compile-target");

        string[] ruleFiles = Directory.GetFiles(
            rulesDirPath,
            searchPattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        if (ruleFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No generated rule files found in '{rulesDirPath}' using pattern '{searchPattern}'.");
        }

        List<ParsedGeneratedRuleFile> parsed = [];
        foreach (string file in ruleFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            ParsedGeneratedRuleFile? item = ParseGeneratedRuleFile(file);
            if (item is not null)
            {
                parsed.Add(item);
            }
        }

        if (parsed.Count == 0)
        {
            throw new InvalidOperationException(
                $"No IRule<T> generated files were detected in '{rulesDirPath}'.");
        }

        string className = !string.IsNullOrWhiteSpace(classOverride)
            ? RuleClassWriter.ToIdentifier(classOverride!)
            : RuleClassWriter.ToIdentifier(Path.GetFileNameWithoutExtension(targetPath));
        string sourceNamespace = parsed.Select(x => x.Namespace)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? "Generated.Handlers";
        string targetNamespace = ResolveNamespaceForMergeOutput(
            namespaceOverride,
            targetPath,
            rulesDirPath,
            sourceNamespace,
            "Generated.Handlers");

        string contextType = ResolveMergedContextType(parsed, contextOverride);

        Dictionary<string, MergedDependencyField> dependencyFields = MergeDependencyFields(parsed);
        List<MergedRuleMethod> mergedRules = MergeRuleMethods(parsed);
        List<string> helperMethods = MergeHelperMethods(parsed);
        HashSet<string> usings = BuildMergedUsings(parsed);

        string output = RenderMergedSourceClass(
            targetNamespace,
            className,
            usings,
            dependencyFields.Values.ToArray(),
            mergedRules,
            helperMethods,
            "merge --rules-dir");

        bool existed = File.Exists(targetPath);
        string? originalContent = existed ? await File.ReadAllTextAsync(targetPath) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        return await AnsiConsole.Status()
            .StartAsync("[yellow]Merging generated rules into source class...[/]", async _ =>
            {
                try
                {
                    await File.WriteAllTextAsync(targetPath, output, Encoding.UTF8);

                    if (compileCheckEnabled)
                    {
                        string compileTarget = ResolveCompileTarget(targetPath, compileTargetOption, context.WorkingDirectory);
                        CompileCheckResult result = await CompileCheckService.BuildAsync(compileTarget);
                        if (!result.Success)
                        {
                            throw new InvalidOperationException(
                                $"Compile check failed (target: {result.TargetPath}, exit code: {result.ExitCode}).{Environment.NewLine}{result.Output}");
                        }
                    }

                    CommandLineWriter.WriteSuccess(
                        $"Merged {mergedRules.Count} generated rule(s) from {parsed.Count} file(s) into '{targetPath}'.");
                    return 0;
                }
                catch (Exception ex)
                {
                    if (existed)
                    {
                        await File.WriteAllTextAsync(targetPath, originalContent ?? string.Empty, Encoding.UTF8);
                    }
                    else if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }

                    CommandLineWriter.WriteError($"Merge from generated rules failed: {ex.Message}");
                    return 1;
                }
            });
    }

    private static async Task<int> RunMergeFromAttributeSourcesAsync(CommandContext context, string sourceDir, string target)
    {
        string sourceDirPath = Path.GetFullPath(sourceDir, context.WorkingDirectory);
        if (!Directory.Exists(sourceDirPath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirPath}");
        }

        string pattern = OptionReader.GetString(context, "pattern", context.Config.Extract.Pattern) ?? "**/*.cs";
        IReadOnlyList<string> excludes = OptionReader.GetCsvList(context, "exclude", context.Config.Extract.ExcludePatterns);
        bool parallel = OptionReader.GetBool(context, "parallel", context.Config.Extract.Parallel);
        string? namespaceOverride = OptionReader.GetString(context, "namespace");
        string? classOverride = OptionReader.GetString(context, "class");
        string? contextOverride = OptionReader.GetString(context, "context");
        bool compileCheckEnabled = OptionReader.GetBool(context, "compile-check", true);
        string? compileTargetOption = OptionReader.GetString(context, "compile-target");
        string targetPath = Path.GetFullPath(target, context.WorkingDirectory);

        IReadOnlyList<string> sourceFiles = SourceDiscoveryService.Discover(
            context.WorkingDirectory,
            source: null,
            sourceDir: sourceDirPath,
            projectPath: null,
            includePattern: pattern,
            excludes: excludes);

        if (sourceFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No source files found in '{sourceDirPath}' with pattern '{pattern}'.");
        }

        string sourceNamespace = TryReadNamespaceFromFiles(sourceFiles)
            ?? context.Config.Extract.Namespace
            ?? "Generated.Rules";
        string inferredNamespaceFromOutput = ResolveNamespaceForMergeOutput(
            namespaceOverride,
            targetPath,
            sourceDirPath,
            sourceNamespace,
            sourceNamespace);
        string extractNamespace = !string.IsNullOrWhiteSpace(namespaceOverride)
            ? namespaceOverride!
            : inferredNamespaceFromOutput;

        IReadOnlyList<ExtractedRuleDefinition> definitions = await RoslynRuleExtractor.ExtractAsync(
            sourceFiles,
            extractNamespace,
            contextOverride,
            parallel,
            CancellationToken.None);

        if (definitions.Count == 0)
        {
            throw new InvalidOperationException(
                $"No methods marked with [MExtractAsRule] were found in '{sourceDirPath}'.");
        }

        string className = !string.IsNullOrWhiteSpace(classOverride)
            ? RuleClassWriter.ToIdentifier(classOverride!)
            : RuleClassWriter.ToIdentifier(Path.GetFileNameWithoutExtension(targetPath));

        string targetNamespace = !string.IsNullOrWhiteSpace(namespaceOverride)
            ? namespaceOverride!
            : inferredNamespaceFromOutput;

        string contextType = ResolveContextTypeFromDefinitions(definitions, contextOverride);
        List<MergedRuleMethod> mergedRules = MergeRuleMethodsFromDefinitions(definitions, contextType);
        Dictionary<string, MergedDependencyField> dependencyFields = MergeDependencyFieldsFromDefinitions(definitions);
        List<string> helperMethods = MergeHelperMethodsFromDefinitions(definitions);
        HashSet<string> usings = BuildMergedUsingsFromDefinitions(definitions);

        string output = RenderMergedSourceClass(
            targetNamespace,
            className,
            usings,
            dependencyFields.Values.ToArray(),
            mergedRules,
            helperMethods,
            "merge --source-dir");

        bool existed = File.Exists(targetPath);
        string? originalContent = existed ? await File.ReadAllTextAsync(targetPath) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        return await AnsiConsole.Status()
            .StartAsync("[yellow]Merging attribute sources into class...[/]", async _ =>
            {
                try
                {
                    await File.WriteAllTextAsync(targetPath, output, Encoding.UTF8);

                    if (compileCheckEnabled)
                    {
                        string compileTarget = ResolveCompileTarget(targetPath, compileTargetOption, context.WorkingDirectory);
                        CompileCheckResult result = await CompileCheckService.BuildAsync(compileTarget);
                        if (!result.Success)
                        {
                            throw new InvalidOperationException(
                                $"Compile check failed (target: {result.TargetPath}, exit code: {result.ExitCode}).{Environment.NewLine}{result.Output}");
                        }
                    }

                    CommandLineWriter.WriteSuccess(
                        $"Merged {mergedRules.Count} attributed rule(s) from {definitions.Count} method(s) into '{targetPath}'.");
                    return 0;
                }
                catch (Exception ex)
                {
                    if (existed)
                    {
                        await File.WriteAllTextAsync(targetPath, originalContent ?? string.Empty, Encoding.UTF8);
                    }
                    else if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }

                    CommandLineWriter.WriteError($"Merge from attributed sources failed: {ex.Message}");
                    return 1;
                }
            });
    }

    private static string ResolveTargetForRulesDir(CommandContext context, string rulesDir, string? target)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            return target!;
        }

        string rulesDirPath = Path.GetFullPath(rulesDir, context.WorkingDirectory);
        DirectoryInfo rulesDirectory = new(rulesDirPath);
        if (!rulesDirectory.Exists)
        {
            return Path.Combine(context.WorkingDirectory, "MergedFromRules.cs");
        }

        DirectoryInfo? targetDirectory = rulesDirectory;
        if (string.Equals(rulesDirectory.Name, "Rules", StringComparison.OrdinalIgnoreCase) && rulesDirectory.Parent is not null)
        {
            targetDirectory = rulesDirectory.Parent;
        }
        else if (rulesDirectory.Parent is not null)
        {
            targetDirectory = rulesDirectory.Parent;
        }

        string? className = OptionReader.GetString(context, "class");
        string fileName = string.IsNullOrWhiteSpace(className)
            ? "MergedFromRules.cs"
            : $"{RuleClassWriter.ToIdentifier(className!)}.cs";
        return Path.Combine(targetDirectory?.FullName ?? context.WorkingDirectory, fileName);
    }

    private static string ResolveTargetForSourceDir(CommandContext context, string sourceDir, string? target)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            return target!;
        }

        string sourceDirPath = Path.GetFullPath(sourceDir, context.WorkingDirectory);
        DirectoryInfo sourceDirectory = new(sourceDirPath);
        DirectoryInfo? targetDirectory = sourceDirectory.Exists ? sourceDirectory : new DirectoryInfo(context.WorkingDirectory);

        string? className = OptionReader.GetString(context, "class");
        string fileName = string.IsNullOrWhiteSpace(className)
            ? "MergedFromAttributes.cs"
            : $"{RuleClassWriter.ToIdentifier(className!)}.cs";
        return Path.Combine(targetDirectory.FullName, fileName);
    }

    private static string ResolveNamespaceForMergeOutput(
        string? namespaceOverride,
        string targetPath,
        string sourceAnchorDirectory,
        string sourceNamespace,
        string fallbackNamespace)
    {
        if (!string.IsNullOrWhiteSpace(namespaceOverride))
        {
            return namespaceOverride!;
        }

        if (TryReadNamespaceFromFile(targetPath, out string existingNamespace))
        {
            return existingNamespace;
        }

        string computed = ComputeNamespaceByRelativePath(
            sourceNamespace,
            sourceAnchorDirectory,
            Path.GetDirectoryName(targetPath) ?? sourceAnchorDirectory);
        if (!string.IsNullOrWhiteSpace(computed))
        {
            return computed;
        }

        return fallbackNamespace;
    }

    private static string? TryReadNamespaceFromFiles(IReadOnlyList<string> files)
    {
        foreach (string file in files)
        {
            if (TryReadNamespaceFromFile(file, out string ns))
            {
                return ns;
            }
        }

        return null;
    }

    private static bool TryReadNamespaceFromFile(string filePath, out string namespaceName)
    {
        namespaceName = string.Empty;
        if (!File.Exists(filePath))
        {
            return false;
        }

        string source = File.ReadAllText(filePath);
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        string? ns = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(x => x.Name.ToString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (string.IsNullOrWhiteSpace(ns))
        {
            return false;
        }

        namespaceName = ns!;
        return true;
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

    private static string ResolveMergedContextType(IReadOnlyList<ParsedGeneratedRuleFile> parsed, string? contextOverride)
    {
        if (!string.IsNullOrWhiteSpace(contextOverride))
        {
            return contextOverride!;
        }

        string[] contexts = [.. parsed.Select(x => x.ContextType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)];

        if (contexts.Length == 1)
        {
            return contexts[0];
        }

        throw new InvalidOperationException(
            $"Multiple context types detected in generated rules ({string.Join(", ", contexts)}). Provide --context.");
    }

    private static string ResolveContextTypeFromDefinitions(
        IReadOnlyList<ExtractedRuleDefinition> definitions,
        string? contextOverride)
    {
        if (!string.IsNullOrWhiteSpace(contextOverride))
        {
            return contextOverride!;
        }

        string[] contexts = [.. definitions
            .Select(x => x.ContextType)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)];

        if (contexts.Length == 1)
        {
            return contexts[0];
        }

        throw new InvalidOperationException(
            $"Multiple context types detected in attribute sources ({string.Join(", ", contexts)}). Provide --context.");
    }

    private static Dictionary<string, MergedDependencyField> MergeDependencyFields(IReadOnlyList<ParsedGeneratedRuleFile> parsed)
    {
        Dictionary<string, MergedDependencyField> merged = new(StringComparer.Ordinal);
        foreach (MergedDependencyField field in parsed
            .SelectMany(x => x.DependencyFields)
            .OrderBy(x => x.FieldName, StringComparer.Ordinal))
        {
            if (merged.TryGetValue(field.FieldName, out MergedDependencyField? existing))
            {
                if (!string.Equals(existing.TypeName, field.TypeName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Dependency field '{field.FieldName}' has conflicting types: '{existing.TypeName}' vs '{field.TypeName}'.");
                }

                continue;
            }

            merged[field.FieldName] = field;
        }

        return merged;
    }

    private static List<MergedRuleMethod> MergeRuleMethods(IReadOnlyList<ParsedGeneratedRuleFile> parsed)
    {
        Dictionary<string, MergedRuleMethod> byCode = new(StringComparer.OrdinalIgnoreCase);
        foreach (MergedRuleMethod method in parsed
            .Select(x => x.RuleMethod)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (byCode.TryGetValue(method.Code, out MergedRuleMethod? existing))
            {
                CommandLineWriter.WriteWarning(
                    $"Duplicate rule code '{method.Code}' detected in '{method.SourceFile}'. Keeping first from '{existing.SourceFile}'.");
                continue;
            }

            byCode[method.Code] = method;
        }

        List<MergedRuleMethod> merged = [.. byCode.Values
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)];

        HashSet<string> usedMethodNames = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < merged.Count; i++)
        {
            MergedRuleMethod method = merged[i];
            string normalized = RuleClassWriter.ToIdentifier(method.MethodName);
            string candidate = normalized;
            int suffix = 2;
            while (!usedMethodNames.Add(candidate))
            {
                candidate = $"{normalized}{suffix}";
                suffix++;
            }

            if (!string.Equals(candidate, method.MethodName, StringComparison.Ordinal))
            {
                merged[i] = method with { MethodName = candidate };
            }
        }

        return merged;
    }

    private static List<MergedRuleMethod> MergeRuleMethodsFromDefinitions(
        IReadOnlyList<ExtractedRuleDefinition> definitions,
        string contextType)
    {
        Dictionary<string, MergedRuleMethod> byCode = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExtractedRuleDefinition definition in definitions
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            MergedRuleMethod method = new(
                Code: definition.Code,
                Order: definition.Order,
                HookPoint: definition.HookPoint,
                DependsOn: definition.DependsOn,
                ContextType: contextType,
                ReturnType: definition.ReturnType,
                MethodName: definition.MethodName,
                IsAsync: definition.IsAsync,
                Parameters: definition.Parameters,
                MethodBody: definition.MethodBody,
                ExpressionBody: definition.ExpressionBody,
                SourceFile: definition.SourceFile);

            if (byCode.TryGetValue(method.Code, out MergedRuleMethod? existing))
            {
                CommandLineWriter.WriteWarning(
                    $"Duplicate rule code '{method.Code}' detected in '{method.SourceFile}'. Keeping first from '{existing.SourceFile}'.");
                continue;
            }

            byCode[method.Code] = method;
        }

        List<MergedRuleMethod> merged = [.. byCode.Values
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)];

        HashSet<string> usedMethodNames = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < merged.Count; i++)
        {
            MergedRuleMethod method = merged[i];
            string normalized = RuleClassWriter.ToIdentifier(method.MethodName);
            string candidate = normalized;
            int suffix = 2;
            while (!usedMethodNames.Add(candidate))
            {
                candidate = $"{normalized}{suffix}";
                suffix++;
            }

            if (!string.Equals(candidate, method.MethodName, StringComparison.Ordinal))
            {
                merged[i] = method with { MethodName = candidate };
            }
        }

        return merged;
    }

    private static Dictionary<string, MergedDependencyField> MergeDependencyFieldsFromDefinitions(
        IReadOnlyList<ExtractedRuleDefinition> definitions)
    {
        Dictionary<string, MergedDependencyField> merged = new(StringComparer.Ordinal);
        foreach (ServiceDependency field in definitions
            .SelectMany(x => x.Dependencies)
            .OrderBy(x => x.FieldName, StringComparer.Ordinal))
        {
            if (merged.TryGetValue(field.FieldName, out MergedDependencyField? existing))
            {
                if (!string.Equals(existing.TypeName, field.TypeName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Dependency field '{field.FieldName}' has conflicting types: '{existing.TypeName}' vs '{field.TypeName}'.");
                }

                continue;
            }

            merged[field.FieldName] = new MergedDependencyField(field.TypeName, field.FieldName);
        }

        return merged;
    }

    private static List<string> MergeHelperMethodsFromDefinitions(IReadOnlyList<ExtractedRuleDefinition> definitions)
    {
        HashSet<string> dedup = new(StringComparer.Ordinal);
        List<string> merged = [];

        foreach (string helper in definitions
            .SelectMany(x => x.HelperMethods)
            .Select(RenderHelperMethod)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (dedup.Add(helper))
            {
                merged.Add(helper);
            }
        }

        return merged;
    }

    private static List<string> MergeHelperMethods(IReadOnlyList<ParsedGeneratedRuleFile> parsed)
    {
        HashSet<string> dedup = new(StringComparer.Ordinal);
        List<string> merged = [];

        foreach (string helper in parsed
            .SelectMany(x => x.HelperMethods)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (dedup.Add(helper))
            {
                merged.Add(helper);
            }
        }

        return merged;
    }

    private static HashSet<string> BuildMergedUsings(IReadOnlyList<ParsedGeneratedRuleFile> parsed)
    {
        HashSet<string> usings = new(StringComparer.Ordinal)
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "using Muonroi.RuleEngine.Abstractions;"
        };

        foreach (string item in parsed.SelectMany(x => x.Usings))
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                usings.Add(NormalizeUsingDirective(item));
            }
        }

        return usings;
    }

    private static HashSet<string> BuildMergedUsingsFromDefinitions(IReadOnlyList<ExtractedRuleDefinition> definitions)
    {
        HashSet<string> usings = new(StringComparer.Ordinal)
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "using Muonroi.RuleEngine.Abstractions;"
        };

        foreach (string item in definitions.SelectMany(x => x.Usings))
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                usings.Add(NormalizeUsingDirective(item));
            }
        }

        return usings;
    }

    private static string RenderMergedSourceClass(
        string ns,
        string className,
        IReadOnlyCollection<string> usings,
        IReadOnlyList<MergedDependencyField> dependencyFields,
        IReadOnlyList<MergedRuleMethod> mergedRules,
        IReadOnlyList<string> helperMethods,
        string sourceTag)
    {
        List<(string TypeName, string FieldName, string ParameterName)> ctorFields = BuildConstructorFields(dependencyFields);
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine($"// GeneratedAtUtc: {DateTime.UtcNow:O}");
        sb.AppendLine($"// Source: {sourceTag}");
        sb.AppendLine("#nullable enable");

        foreach (string u in usings.OrderBy(x => x, StringComparer.Ordinal))
        {
            sb.AppendLine(NormalizeUsingDirective(u));
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {className}");
        sb.AppendLine("{");

        foreach (MergedDependencyField field in dependencyFields.OrderBy(x => x.FieldName, StringComparer.Ordinal))
        {
            sb.AppendLine($"    private readonly {field.TypeName} {field.FieldName};");
        }

        if (dependencyFields.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    public {className}({string.Join(", ", ctorFields.Select(x => $"{x.TypeName} {x.ParameterName}"))})");
            sb.AppendLine("    {");
            foreach ((string _, string fieldName, string paramName) in ctorFields)
            {
                sb.AppendLine($"        this.{fieldName} = {paramName};");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (MergedRuleMethod rule in mergedRules)
        {
            sb.AppendLine(IndentBlock(RenderMergedMethod(rule), 1));
            sb.AppendLine();
        }

        foreach (string helper in helperMethods)
        {
            sb.AppendLine(IndentBlock(helper, 1));
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static List<(string TypeName, string FieldName, string ParameterName)> BuildConstructorFields(
        IReadOnlyList<MergedDependencyField> fields)
    {
        HashSet<string> usedParams = new(StringComparer.Ordinal);
        List<(string TypeName, string FieldName, string ParameterName)> result = [];
        foreach (MergedDependencyField field in fields.OrderBy(x => x.FieldName, StringComparer.Ordinal))
        {
            string param = BuildConstructorParameterName(field.FieldName, usedParams);
            result.Add((field.TypeName, field.FieldName, param));
        }

        return result;
    }

    private static string BuildConstructorParameterName(string fieldName, ISet<string> usedParams)
    {
        string trimmed = fieldName.TrimStart('_');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            trimmed = "value";
        }

        string baseName = $"p{char.ToUpperInvariant(trimmed[0])}{trimmed[1..]}";
        string candidate = baseName;
        int suffix = 2;
        while (!usedParams.Add(candidate))
        {
            candidate = $"{baseName}{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string RenderMergedMethod(MergedRuleMethod method)
    {
        string methodName = RuleClassWriter.ToIdentifier(method.MethodName);
        string dependsOn = method.DependsOn.Count == 0
            ? string.Empty
            : $", DependsOn = new[] {{ {string.Join(", ", method.DependsOn.Select(x => $"\"{x}\""))} }}";
        string asyncModifier = method.IsAsync ? "async " : string.Empty;
        string parameters = string.Join(", ", method.Parameters.Select(RenderParameter));
        string hookPoint = NormalizeHookPoint(method.HookPoint);

        StringBuilder sb = new();
        sb.AppendLine($"[MExtractAsRule(\"{method.Code}\", Order = {method.Order}, HookPoint = HookPoint.{hookPoint}{dependsOn})]");
        sb.AppendLine($"public {asyncModifier}{method.ReturnType} {methodName}({parameters})");
        sb.AppendLine("{");

        if (!string.IsNullOrWhiteSpace(method.MethodBody))
        {
            foreach (string line in ExtractBodyLines(method.MethodBody!))
            {
                sb.AppendLine($"    {line}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(method.ExpressionBody))
        {
            sb.AppendLine($"    return {method.ExpressionBody};");
        }
        else
        {
            sb.AppendLine("    return default!;");
        }

        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    private static string RenderHelperMethod(HelperMethodDefinition helper)
    {
        string methodName = RuleClassWriter.ToIdentifier(helper.MethodName);
        string asyncModifier = helper.IsAsync ? "async " : string.Empty;
        string staticModifier = helper.IsStatic ? "static " : string.Empty;
        string parameters = string.Join(", ", helper.Parameters.Select(RenderParameter));

        StringBuilder sb = new();
        sb.AppendLine($"private {staticModifier}{asyncModifier}{helper.ReturnType} {methodName}({parameters})");
        sb.AppendLine("{");

        if (!string.IsNullOrWhiteSpace(helper.MethodBody))
        {
            foreach (string line in ExtractBodyLines(helper.MethodBody!))
            {
                sb.AppendLine($"    {line}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(helper.ExpressionBody))
        {
            sb.AppendLine($"    return {helper.ExpressionBody};");
        }
        else
        {
            sb.AppendLine("    throw new NotImplementedException();");
        }

        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    private static ParsedGeneratedRuleFile? ParseGeneratedRuleFile(string filePath)
    {
        string source = File.ReadAllText(filePath);
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

        ClassDeclarationSyntax? classNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => TryResolveContextType(c, out _));

        if (classNode is null)
        {
            return null;
        }

        if (!TryResolveContextType(classNode, out string? contextType))
        {
            return null;
        }

        MethodDeclarationSyntax evaluate = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => string.Equals(m.Identifier.ValueText, "EvaluateAsync", StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Generated rule class '{classNode.Identifier.ValueText}' missing EvaluateAsync ({filePath}).");

        LocalFunctionStatementSyntax? localFunction = evaluate.DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText.StartsWith("__source_", StringComparison.Ordinal));

        string code = TryReadStringProperty(classNode, "Code")
            ?? RuleClassWriter.ToIdentifier(Path.GetFileNameWithoutExtension(filePath).Replace("Rule", string.Empty, StringComparison.OrdinalIgnoreCase));
        int order = TryReadIntProperty(classNode, "Order") ?? 0;
        string hookPoint = TryReadHookPoint(classNode);
        List<string> dependsOn = TryReadDependsOn(classNode);

        string methodName;
        IReadOnlyList<ParameterModel> parameters;
        string returnType;
        bool isAsync;
        string? methodBody;
        string? expressionBody;

        if (localFunction is not null)
        {
            methodName = localFunction.Identifier.ValueText;
            if (methodName.StartsWith("__source_", StringComparison.Ordinal))
            {
                methodName = methodName["__source_".Length..];
            }

            parameters = [.. localFunction.ParameterList.Parameters.Select(ParseParameter)];
            returnType = localFunction.ReturnType.ToString();
            isAsync = localFunction.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
            methodBody = localFunction.Body?.ToFullString();
            expressionBody = localFunction.ExpressionBody?.Expression.ToString();
        }
        else
        {
            methodName = RuleClassWriter.ToIdentifier(
                classNode.Identifier.ValueText.EndsWith("Rule", StringComparison.OrdinalIgnoreCase)
                    ? classNode.Identifier.ValueText[..^4]
                    : classNode.Identifier.ValueText);
            parameters = [.. evaluate.ParameterList.Parameters.Select(ParseParameter)];
            returnType = evaluate.ReturnType.ToString();
            isAsync = evaluate.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
            methodBody = evaluate.Body?.ToFullString();
            expressionBody = evaluate.ExpressionBody?.Expression.ToString();
        }

        List<MergedDependencyField> dependencyFields = ParseDependencyFields(classNode);
        List<string> helperMethods = [.. classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => !string.Equals(m.Identifier.ValueText, "EvaluateAsync", StringComparison.Ordinal) &&
                        !string.Equals(m.Identifier.ValueText, "ExecuteAsync", StringComparison.Ordinal))
            .Select(m => m.NormalizeWhitespace().ToFullString())];

        string ns = classNode.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
            ?? "Generated.Handlers";
        string[] usings = [.. root.Usings
            .Select(x => x.ToString().Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)];

        MergedRuleMethod rule = new(
            Code: code,
            Order: order,
            HookPoint: hookPoint,
            DependsOn: dependsOn,
            ContextType: contextType!,
            ReturnType: returnType,
            MethodName: methodName,
            IsAsync: isAsync,
            Parameters: parameters,
            MethodBody: methodBody,
            ExpressionBody: expressionBody,
            SourceFile: filePath);

        return new ParsedGeneratedRuleFile(
            Namespace: ns,
            ContextType: contextType!,
            Usings: usings,
            DependencyFields: dependencyFields,
            HelperMethods: helperMethods,
            RuleMethod: rule);
    }

    private static bool TryResolveContextType(ClassDeclarationSyntax classNode, out string? contextType)
    {
        contextType = null;
        if (classNode.BaseList is null)
        {
            return false;
        }

        foreach (BaseTypeSyntax baseType in classNode.BaseList.Types)
        {
            if (baseType.Type is GenericNameSyntax generic &&
                string.Equals(generic.Identifier.ValueText, "IRule", StringComparison.Ordinal) &&
                generic.TypeArgumentList.Arguments.Count == 1)
            {
                contextType = generic.TypeArgumentList.Arguments[0].ToString();
                return true;
            }

            if (baseType.Type is QualifiedNameSyntax qualified &&
                qualified.Right is GenericNameSyntax qGeneric &&
                string.Equals(qGeneric.Identifier.ValueText, "IRule", StringComparison.Ordinal) &&
                qGeneric.TypeArgumentList.Arguments.Count == 1)
            {
                contextType = qGeneric.TypeArgumentList.Arguments[0].ToString();
                return true;
            }
        }

        return false;
    }

    private static string? TryReadStringProperty(ClassDeclarationSyntax classNode, string propertyName)
    {
        ExpressionSyntax? expr = TryReadPropertyExpression(classNode, propertyName);
        return expr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }

    private static int? TryReadIntProperty(ClassDeclarationSyntax classNode, string propertyName)
    {
        ExpressionSyntax? expr = TryReadPropertyExpression(classNode, propertyName);
        if (expr is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.NumericLiteralExpression) &&
            literal.Token.Value is int value)
        {
            return value;
        }

        return int.TryParse(expr?.ToString(), out int parsed) ? parsed : null;
    }

    private static string TryReadHookPoint(ClassDeclarationSyntax classNode)
    {
        ExpressionSyntax? expr = TryReadPropertyExpression(classNode, "HookPoint");
        if (expr is MemberAccessExpressionSyntax member)
        {
            return member.Name.Identifier.ValueText;
        }

        string raw = expr?.ToString() ?? string.Empty;
        int idx = raw.LastIndexOf('.');
        if (idx >= 0 && idx < raw.Length - 1)
        {
            return raw[(idx + 1)..].Trim();
        }

        return string.IsNullOrWhiteSpace(raw) ? HookPoint.BeforeRule.ToString() : raw.Trim();
    }

    private static List<string> TryReadDependsOn(ClassDeclarationSyntax classNode)
    {
        ExpressionSyntax? expr = TryReadPropertyExpression(classNode, "DependsOn");
        if (expr is null)
        {
            return [];
        }

        if (expr is InvocationExpressionSyntax invocation &&
            invocation.Expression.ToString().StartsWith("Array.Empty", StringComparison.Ordinal))
        {
            return [];
        }

        InitializerExpressionSyntax? initializer = expr switch
        {
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
            ArrayCreationExpressionSyntax arrayCreation => arrayCreation.Initializer,
            _ => null
        };

        if (initializer is null)
        {
            return [];
        }

        return [.. initializer.Expressions
            .OfType<LiteralExpressionSyntax>()
            .Where(x => x.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(x => x.Token.ValueText)
            .Where(x => !string.IsNullOrWhiteSpace(x))];
    }

    private static ExpressionSyntax? TryReadPropertyExpression(ClassDeclarationSyntax classNode, string propertyName)
    {
        PropertyDeclarationSyntax? property = classNode.Members
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => string.Equals(p.Identifier.ValueText, propertyName, StringComparison.Ordinal));
        if (property is null)
        {
            return null;
        }

        if (property.ExpressionBody is not null)
        {
            return property.ExpressionBody.Expression;
        }

        AccessorDeclarationSyntax? getter = property.AccessorList?.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter?.ExpressionBody is not null)
        {
            return getter.ExpressionBody.Expression;
        }

        ReturnStatementSyntax? returnStatement = getter?.Body?.Statements
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault();
        return returnStatement?.Expression;
    }

    private static List<MergedDependencyField> ParseDependencyFields(ClassDeclarationSyntax classNode)
    {
        Dictionary<string, MergedDependencyField> fields = new(StringComparer.Ordinal);
        foreach (FieldDeclarationSyntax field in classNode.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!field.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)) ||
                !field.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)) ||
                field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            {
                continue;
            }

            string typeName = field.Declaration.Type.ToString();
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                string fieldName = variable.Identifier.ValueText;
                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    continue;
                }

                if (fields.TryGetValue(fieldName, out MergedDependencyField? existing))
                {
                    if (!string.Equals(existing.TypeName, typeName, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Field '{fieldName}' has conflicting types in class '{classNode.Identifier.ValueText}': '{existing.TypeName}' vs '{typeName}'.");
                    }

                    continue;
                }

                fields[fieldName] = new MergedDependencyField(typeName, fieldName);
            }
        }

        if (classNode.ParameterList is not null)
        {
            foreach (ParameterSyntax parameter in classNode.ParameterList.Parameters)
            {
                if (parameter.Type is null)
                {
                    continue;
                }

                string fieldName = parameter.Identifier.ValueText;
                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    continue;
                }

                string typeName = parameter.Type.ToString();
                if (fields.TryGetValue(fieldName, out MergedDependencyField? existing))
                {
                    if (!string.Equals(existing.TypeName, typeName, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Primary constructor dependency '{fieldName}' has conflicting types in class '{classNode.Identifier.ValueText}': '{existing.TypeName}' vs '{typeName}'.");
                    }

                    continue;
                }

                fields[fieldName] = new MergedDependencyField(typeName, fieldName);
            }
        }

        return [.. fields.Values.OrderBy(x => x.FieldName, StringComparer.Ordinal)];
    }

    private static ParameterModel ParseParameter(ParameterSyntax parameter)
    {
        string name = parameter.Identifier.ValueText;
        string typeName = parameter.Type?.ToString() ?? "object";
        bool hasDefault = parameter.Default is not null;
        string? defaultExpression = parameter.Default?.Value.ToString();
        return new ParameterModel(name, typeName, hasDefault, defaultExpression);
    }

    private static string RenderParameter(ParameterModel p)
    {
        if (p.HasDefaultValue && !string.IsNullOrWhiteSpace(p.DefaultValueExpression))
        {
            return $"{p.TypeName} {p.Name} = {p.DefaultValueExpression}";
        }

        return $"{p.TypeName} {p.Name}";
    }

    private static string[] ExtractBodyLines(string methodBody)
    {
        string body = methodBody.Trim();
        if (body.StartsWith("{", StringComparison.Ordinal) &&
            body.EndsWith("}", StringComparison.Ordinal) &&
            body.Length >= 2)
        {
            body = body[1..^1];
        }

        List<string> lines = [.. body
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(x => x.TrimEnd())];

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return [];
        }

        int minIndent = lines
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(GetLeadingWhitespaceCount)
            .DefaultIfEmpty(0)
            .Min();

        return [.. lines.Select(x =>
            x.Length >= minIndent ? x[minIndent..] : x)];
    }

    private static int GetLeadingWhitespaceCount(string value)
    {
        int count = 0;
        while (count < value.Length && char.IsWhiteSpace(value[count]))
        {
            count++;
        }

        return count;
    }

    private static string NormalizeUsingDirective(string value)
    {
        string normalized = value.Trim();
        if (!normalized.StartsWith("using ", StringComparison.Ordinal))
        {
            normalized = $"using {normalized}";
        }

        if (!normalized.EndsWith(";", StringComparison.Ordinal))
        {
            normalized += ";";
        }

        return normalized;
    }

    private sealed record ParsedGeneratedRuleFile(
        string Namespace,
        string ContextType,
        IReadOnlyList<string> Usings,
        IReadOnlyList<MergedDependencyField> DependencyFields,
        IReadOnlyList<string> HelperMethods,
        MergedRuleMethod RuleMethod);

    private sealed record MergedDependencyField(string TypeName, string FieldName);

    private sealed record MergedRuleMethod(
        string Code,
        int Order,
        string HookPoint,
        IReadOnlyList<string> DependsOn,
        string ContextType,
        string ReturnType,
        string MethodName,
        bool IsAsync,
        IReadOnlyList<ParameterModel> Parameters,
        string? MethodBody,
        string? ExpressionBody,
        string SourceFile);

    private static string NormalizeHookPoint(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out HookPoint parsed)
            ? parsed.ToString()
            : HookPoint.BeforeRule.ToString();
    }
}
