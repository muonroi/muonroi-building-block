using Muonroi.RuleGen.Mcp.Models;

namespace Muonroi.RuleGen.Mcp.Infrastructure;

public sealed class ComplianceScanner
{
    internal async Task<ComplianceCheckResult> ScanAsync(
        IReadOnlyList<string> paths,
        string includePattern,
        IReadOnlyList<string> excludePatterns,
        CancellationToken cancellationToken)
    {
        ScanTargetSet targets = ExpandTargets(paths, includePattern, excludePatterns);
        if (targets.CSharpFiles.Count == 0 && targets.ProjectFiles.Count == 0)
        {
            return new ComplianceCheckResult([], 0, 0, 0, "none", [], ["No C# or project files matched the provided paths."]);
        }

        List<ComplianceViolation> violations = [];
        List<string> notes = [];
        List<string> analyzedFiles = [];

        foreach (string file in targets.CSharpFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            analyzedFiles.Add(file);
            string source = await File.ReadAllTextAsync(file, cancellationToken);
            violations.AddRange(SyntaxScan(file, source));
        }

        foreach (string projectFile in targets.ProjectFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            analyzedFiles.Add(projectFile);
            string content = await File.ReadAllTextAsync(projectFile, cancellationToken);
            violations.AddRange(ProjectScan(projectFile, content));
        }

        int warningCount = violations.Count(v => string.Equals(v.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        int errorCount = violations.Count(v => string.Equals(v.Severity, "Error", StringComparison.OrdinalIgnoreCase));
        int failedFiles = violations
            .Where(v => !string.IsNullOrWhiteSpace(v.File))
            .Select(v => Path.GetFullPath(v.File!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        int passedFiles = Math.Max(0, analyzedFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() - failedFiles);

        notes.Add("Compliance scan uses deterministic Muonroi MCP pattern/project checks for MBB001-MBB007.");
        return new ComplianceCheckResult(
            violations.OrderBy(v => v.File, StringComparer.OrdinalIgnoreCase).ThenBy(v => v.Line).ThenBy(v => v.Column).ToArray(),
            errorCount,
            warningCount,
            passedFiles,
            "pattern+project",
            analyzedFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            notes);
    }

    private static IReadOnlyList<ComplianceViolation> SyntaxScan(string file, string source)
    {
        List<ComplianceViolation> violations = [];
        string[] lines = source.Split(["\r\n", "\n"], StringSplitOptions.None);
        string normalizedPath = Path.GetFullPath(file);
        string namespaceName = ReadNamespace(source);

        AddPatternViolations(source, lines, normalizedPath, "MBB001", new[] { "DateTime.UtcNow", "DateTime.Now", "DateTime.Today" }, violations,
            path => path.EndsWith("MDateTimeService.cs", StringComparison.OrdinalIgnoreCase) ||
                    namespaceName.Contains("Muonroi.Core", StringComparison.Ordinal));
        AddPatternViolations(source, lines, normalizedPath, "MBB002", new[] { "JsonSerializer.Serialize", "JsonSerializer.Deserialize" }, violations,
            path => path.EndsWith("DecisionTableJsonSerializer.cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("DecisionTableToJsonConverter.cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) && path.Contains($"{Path.DirectorySeparatorChar}PolicySigner{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        AddPatternViolations(source, lines, normalizedPath, "MBB003", new[] { ": DbContext" }, violations);
        AddPatternViolations(source, lines, normalizedPath, "MBB004", new[] { "AsyncLocal<" }, violations,
            _ => namespaceName.Contains("Muonroi.Core.Abstractions.Context", StringComparison.Ordinal));
        AddTierGuardViolations(lines, normalizedPath, violations);
        AddPatternViolations(source, lines, normalizedPath, "MBB007", new[] { "LogContext.PushProperty" }, violations,
            _ => namespaceName.Contains("Muonroi.Logging", StringComparison.Ordinal) ||
                 namespaceName.Contains("Muonroi.Observability", StringComparison.Ordinal));

        return violations;
    }

    private static IReadOnlyList<ComplianceViolation> ProjectScan(string projectFile, string content)
    {
        List<ComplianceViolation> violations = [];
        string projectName = Path.GetFileNameWithoutExtension(projectFile);
        if (!projectName.EndsWith(".Abstractions", StringComparison.OrdinalIgnoreCase))
        {
            return violations;
        }

        EcosystemRuleDescriptor rule = EcosystemCatalog.Rules.First(x => x.Code == "MBB005");
        string[] forbiddenReferences =
        [
            "Microsoft.EntityFrameworkCore",
            "Hangfire",
            "Quartz",
            "MassTransit",
            "Serilog",
            "RabbitMQ.Client",
            "Confluent.Kafka",
            "Muonroi.Data.EntityFrameworkCore",
            "Muonroi.BackgroundJobs.Hangfire",
            "Muonroi.BackgroundJobs.Quartz",
            "Muonroi.Messaging.MassTransit"
        ];

        foreach (string reference in forbiddenReferences.Where(content.Contains))
        {
            (int? line, int? column, string? rawCode) = FindFirstMatch(content, reference);
            violations.Add(new ComplianceViolation(
                "MBB005",
                rule.Severity,
                projectFile,
                line,
                column,
                $"Abstractions project '{projectName}' references infrastructure dependency '{reference}'.",
                rule.SuggestedFix,
                rawCode,
                rule.ExemptComment));
        }

        return violations;
    }

    private static void AddPatternViolations(
        string source,
        string[] lines,
        string file,
        string code,
        IReadOnlyList<string> patterns,
        List<ComplianceViolation> violations,
        Func<string, bool>? skipFile = null)
    {
        if (skipFile?.Invoke(file) == true)
        {
            return;
        }

        EcosystemRuleDescriptor rule = EcosystemCatalog.Rules.First(x => x.Code == code);
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (IsExempt(lines[lineIndex], code))
            {
                continue;
            }

            foreach (string pattern in patterns)
            {
                int column = lines[lineIndex].IndexOf(pattern, StringComparison.Ordinal);
                if (column < 0)
                {
                    continue;
                }

                violations.Add(new ComplianceViolation(
                    code,
                    rule.Severity,
                    file,
                    lineIndex + 1,
                    column + 1,
                    $"Fallback scan detected forbidden pattern '{pattern}'.",
                    rule.SuggestedFix,
                    lines[lineIndex].Trim(),
                    rule.ExemptComment));
            }
        }
    }

    private static void AddTierGuardViolations(string[] lines, string file, List<ComplianceViolation> violations)
    {
        EcosystemRuleDescriptor rule = EcosystemCatalog.Rules.First(x => x.Code == "MBB006");
        string[] guardedCalls =
        [
            "AddMassTransit(",
            "AddGrpcServer(",
            "AddRedis(",
            "AddMessageBus(",
            "AddRuleEngineStore(",
            "AddObservability("
        ];

        bool hasTierGuard = lines.Any(line => line.Contains("EnsureFeatureOrThrow(", StringComparison.Ordinal));
        if (hasTierGuard)
        {
            return;
        }

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (IsExempt(lines[lineIndex], "MBB006"))
            {
                continue;
            }

            string? matched = guardedCalls.FirstOrDefault(lines[lineIndex].Contains);
            if (matched is null)
            {
                continue;
            }

            int column = lines[lineIndex].IndexOf(matched, StringComparison.Ordinal);
            violations.Add(new ComplianceViolation(
                "MBB006",
                rule.Severity,
                file,
                lineIndex + 1,
                column + 1,
                $"Feature registration '{matched[..^1]}' must be guarded by EnsureFeatureOrThrow(...).",
                rule.SuggestedFix,
                lines[lineIndex].Trim(),
                rule.ExemptComment));
        }
    }

    private static ScanTargetSet ExpandTargets(IReadOnlyList<string> paths, string includePattern, IReadOnlyList<string> excludePatterns)
    {
        HashSet<string> csharpFiles = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> projectFiles = new(StringComparer.OrdinalIgnoreCase);

        foreach (string rawPath in paths)
        {
            string path = Path.GetFullPath(rawPath);
            if (File.Exists(path) && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                csharpFiles.Add(path);
                continue;
            }

            if (File.Exists(path) && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                projectFiles.Add(path);
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            string searchPattern = NormalizeSearchPattern(includePattern);
            foreach (string file in Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories))
            {
                if (excludePatterns.Any(pattern => MatchesExclude(file, pattern)))
                {
                    continue;
                }

                csharpFiles.Add(Path.GetFullPath(file));
            }

            foreach (string projectFile in Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories))
            {
                if (excludePatterns.Any(pattern => MatchesExclude(projectFile, pattern)))
                {
                    continue;
                }

                projectFiles.Add(Path.GetFullPath(projectFile));
            }
        }

        return new ScanTargetSet(csharpFiles, projectFiles);
    }

    private static (int? Line, int? Column, string? RawCode) FindFirstMatch(string content, string token)
    {
        string[] lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        for (int index = 0; index < lines.Length; index++)
        {
            int column = lines[index].IndexOf(token, StringComparison.Ordinal);
            if (column >= 0)
            {
                return (index + 1, column + 1, lines[index].Trim());
            }
        }

        return (null, null, null);
    }

    private static string ReadNamespace(string source)
    {
        foreach (string line in source.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("namespace ", StringComparison.Ordinal))
            {
                return trimmed["namespace ".Length..].Trim().TrimEnd(';');
            }
        }

        return string.Empty;
    }

    private static bool IsExempt(string line, string code)
    {
        return line.Contains($"// {code}-exempt", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchPattern(string includePattern)
    {
        if (string.IsNullOrWhiteSpace(includePattern) || string.Equals(includePattern, "**/*.cs", StringComparison.Ordinal))
        {
            return "*.cs";
        }

        string normalized = includePattern.Replace("**/", string.Empty, StringComparison.Ordinal)
            .Replace("**\\", string.Empty, StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(normalized) ? "*.cs" : normalized;
    }

    private static bool MatchesExclude(string path, string pattern)
    {
        string normalized = pattern.Replace("**/", string.Empty, StringComparison.Ordinal)
            .Replace("/**", string.Empty, StringComparison.Ordinal)
            .Replace("**\\", string.Empty, StringComparison.Ordinal)
            .Replace("\\**", string.Empty, StringComparison.Ordinal)
            .Trim();
        return !string.IsNullOrWhiteSpace(normalized) && path.Contains(normalized, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record ScanTargetSet(
    HashSet<string> CSharpFiles,
    HashSet<string> ProjectFiles);
