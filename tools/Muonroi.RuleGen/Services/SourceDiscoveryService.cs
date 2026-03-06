using Microsoft.Extensions.FileSystemGlobbing;
using System.Xml.Linq;

namespace Muonroi.RuleGen.Services;

internal static class SourceDiscoveryService
{
    public static IReadOnlyList<string> Discover(
        string workingDirectory,
        string? source,
        string? sourceDir,
        string? projectPath,
        string includePattern,
        IReadOnlyList<string> excludes)
    {
        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            string resolved = ResolvePath(workingDirectory, projectPath);
            if (!File.Exists(resolved))
            {
                throw new FileNotFoundException($"Project file not found: {resolved}");
            }

            foreach (string file in DiscoverFromProject(resolved, includePattern, excludes))
            {
                files.Add(file);
            }
        }

        string? effectiveSourceDir = !string.IsNullOrWhiteSpace(sourceDir)
            ? ResolvePath(workingDirectory, sourceDir)
            : null;

        if (!string.IsNullOrWhiteSpace(effectiveSourceDir))
        {
            if (!Directory.Exists(effectiveSourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {effectiveSourceDir}");
            }

            foreach (string file in DiscoverFromDirectory(effectiveSourceDir, includePattern, excludes))
            {
                files.Add(file);
            }
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            string resolvedSource = ResolvePath(workingDirectory, source);
            if (File.Exists(resolvedSource))
            {
                files.Add(resolvedSource);
            }
            else if (Directory.Exists(resolvedSource))
            {
                foreach (string file in DiscoverFromDirectory(resolvedSource, includePattern, excludes))
                {
                    files.Add(file);
                }
            }
            else
            {
                throw new FileNotFoundException($"Source path not found: {resolvedSource}");
            }
        }

        if (files.Count == 0)
        {
            throw new InvalidOperationException("No source files discovered. Provide --source, --source-dir, or --project.");
        }

        return files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> DiscoverFromProject(string projectPath, string includePattern, IReadOnlyList<string> excludes)
    {
        string projectDir = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
        XDocument doc = XDocument.Load(projectPath);

        List<string> compileIncludes = [.. doc.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "Compile", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()];

        if (compileIncludes.Count == 0)
        {
            return DiscoverFromDirectory(projectDir, includePattern, excludes);
        }

        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (string? include in compileIncludes)
        {
            string full = ResolvePath(projectDir, include);
            if (File.Exists(full) && full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(full);
                continue;
            }

            string rooted = include.Replace('\\', '/');
            if (rooted.Contains('*'))
            {
                Matcher matcher = new(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(rooted);
                matcher.AddExclude("**/bin/**");
                matcher.AddExclude("**/obj/**");
                matcher.AddExclude("**/*.g.cs");
                matcher.AddExclude("**/*.Generated.cs");
                foreach (string ex in excludes)
                {
                    matcher.AddExclude(NormalizePattern(ex));
                }

                foreach (string match in matcher.GetResultsInFullPath(projectDir))
                {
                    if (match.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(match);
                    }
                }
            }
        }

        return files;
    }

    private static IEnumerable<string> DiscoverFromDirectory(string sourceDir, string includePattern, IReadOnlyList<string> excludes)
    {
        Matcher matcher = new(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(NormalizePattern(includePattern));
        matcher.AddExclude("**/bin/**");
        matcher.AddExclude("**/obj/**");
        matcher.AddExclude("**/*.g.cs");
        matcher.AddExclude("**/*.Generated.cs");

        foreach (string ex in excludes)
        {
            matcher.AddExclude(NormalizePattern(ex));
        }

        foreach (string file in matcher.GetResultsInFullPath(sourceDir))
        {
            if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private static string ResolvePath(string baseDir, string path)
    {
        return Path.GetFullPath(path, baseDir);
    }

    private static string NormalizePattern(string pattern)
    {
        return pattern.Replace('\\', '/');
    }
}
