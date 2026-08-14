using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var dryRun = args.Contains("--dry-run");
var repoRoot = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) is { } explicitRoot
    ? Path.GetFullPath(explicitRoot)
    : FindRepoRoot(Directory.GetCurrentDirectory());

var includeTopDirs = new[] { "src", "tests", "samples", "tools" };
var excludeDirNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules" };

Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine(dryRun ? "Mode: dry-run (no files written)" : "Mode: write");

var allCsproj = includeTopDirs
    .Select(t => Path.Combine(repoRoot, t))
    .Where(Directory.Exists)
    .SelectMany(d => Directory.EnumerateFiles(d, "*.csproj", SearchOption.AllDirectories))
    .Where(p => !PathContainsExcludedDir(p, excludeDirNames))
    .Select(Path.GetFullPath)
    .OrderBy(p => p, StringComparer.Ordinal)
    .ToList();

var projectDirs = allCsproj
    .Select(p => Path.GetFullPath(Path.GetDirectoryName(p)!))
    .OrderByDescending(d => d.Length)
    .ToList();

var skippedFiles = new List<string>();
var touchedProjects = new List<(string Project, int FilesChanged, int UsingsAdded)>();
int totalFilesChanged = 0;
int totalUsingsAdded = 0;

foreach (var csproj in allCsproj)
{
    var projDir = Path.GetFullPath(Path.GetDirectoryName(csproj)!);

    var csFiles = Directory.EnumerateFiles(projDir, "*.cs", SearchOption.AllDirectories)
        .Where(f => !PathContainsExcludedDir(f, excludeDirNames))
        .Where(f => !string.Equals(Path.GetFileName(f), "GlobalUsings.cs", StringComparison.OrdinalIgnoreCase))
        .Where(f => !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
        .Where(f => !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
        .Where(f => OwningProjectDir(f, projectDirs) == projDir)
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToList();

    var namesToAdd = new List<string>();
    var addedSet = new HashSet<string>(StringComparer.Ordinal);
    var filesChangedThisProject = 0;

    foreach (var file in csFiles)
    {
        var original = File.ReadAllText(file);
        if (!original.Contains("using ", StringComparison.Ordinal))
        {
            continue;
        }

        if (original.Contains("#if", StringComparison.Ordinal))
        {
            skippedFiles.Add($"{Rel(repoRoot, file)}: skipped (contains #if, using directives may be conditional)");
            continue;
        }

        var tree = CSharpSyntaxTree.ParseText(original, path: file);
        var root = (CompilationUnitSyntax)tree.GetRoot();

        var namespaceScopedUsings = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .SelectMany(n => n.Usings)
            .Any();
        if (namespaceScopedUsings)
        {
            skippedFiles.Add($"{Rel(repoRoot, file)}: skipped (namespace-scoped using directives)");
            continue;
        }

        var candidates = root.Usings
            .Where(u => u.Alias is null)
            .Where(u => u.StaticKeyword.IsKind(SyntaxKind.None))
            .Where(u => !HasCommentTrivia(u))
            .ToList();

        if (candidates.Count == 0)
        {
            continue;
        }

        var newRoot = root.RemoveNodes(candidates, SyntaxRemoveOptions.KeepNoTrivia)!;
        var newText = TrimLeadingBlankLines(newRoot.ToFullString());

        if (newText == original)
        {
            continue;
        }

        if (!dryRun)
        {
            File.WriteAllText(file, newText, new UTF8Encoding(false));
        }

        filesChangedThisProject++;
        foreach (var name in candidates.Select(u => u.Name!.ToString()))
        {
            if (addedSet.Add(name))
            {
                namesToAdd.Add(name);
            }
        }
    }

    if (filesChangedThisProject == 0)
    {
        continue;
    }

    var globalUsingsPath = Path.Combine(projDir, "GlobalUsings.cs");
    var existingLines = File.Exists(globalUsingsPath)
        ? File.ReadAllLines(globalUsingsPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
        : [];
    var existingBodies = new HashSet<string>(
        existingLines.Select(NormalizeGlobalUsingBody).Where(b => b is not null)!,
        StringComparer.Ordinal);

    var appended = 0;
    var insertAt = existingLines.FindLastIndex(l => l.TrimStart().StartsWith("global using", StringComparison.Ordinal)) + 1;
    foreach (var name in namesToAdd)
    {
        if (existingBodies.Contains(name))
        {
            continue;
        }

        existingLines.Insert(insertAt, $"global using {name};");
        insertAt++;
        existingBodies.Add(name);
        appended++;
    }

    if (!dryRun && (appended > 0 || !File.Exists(globalUsingsPath)))
    {
        File.WriteAllText(
            globalUsingsPath,
            string.Join(Environment.NewLine, existingLines) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    totalFilesChanged += filesChangedThisProject;
    totalUsingsAdded += appended;
    touchedProjects.Add((Rel(repoRoot, csproj), filesChangedThisProject, appended));
}

Console.WriteLine();
Console.WriteLine("=== Summary ===");
foreach (var (project, filesChanged, usingsAdded) in touchedProjects)
{
    Console.WriteLine($"{project}: {filesChanged} file(s) changed, {usingsAdded} using(s) newly globalized");
}

Console.WriteLine();
Console.WriteLine($"Projects touched: {touchedProjects.Count}/{allCsproj.Count}");
Console.WriteLine($"Files changed: {totalFilesChanged}");
Console.WriteLine($"Usings newly globalized: {totalUsingsAdded}");

if (skippedFiles.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"=== Skipped files ({skippedFiles.Count}) ===");
    foreach (var line in skippedFiles)
    {
        Console.WriteLine(line);
    }
}

return 0;

static bool PathContainsExcludedDir(string path, HashSet<string> excludeDirNames)
{
    return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(excludeDirNames.Contains);
}

static string? OwningProjectDir(string filePath, List<string> projectDirsLongestFirst)
{
    var fileDir = Path.GetFullPath(Path.GetDirectoryName(filePath)!);
    foreach (var dir in projectDirsLongestFirst)
    {
        if (fileDir.Equals(dir, StringComparison.OrdinalIgnoreCase)
            || fileDir.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return dir;
        }
    }

    return null;
}

static bool HasCommentTrivia(UsingDirectiveSyntax u)
{
    return u.GetLeadingTrivia().Any(IsComment) || u.GetTrailingTrivia().Any(IsComment);

    static bool IsComment(SyntaxTrivia t) =>
        t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia);
}

static string TrimLeadingBlankLines(string text) =>
    Regex.Replace(text, @"\A(?:[ \t]*\r?\n)+", string.Empty);

static string? NormalizeGlobalUsingBody(string line)
{
    var trimmed = line.Trim();
    const string prefix = "global using ";
    if (!trimmed.StartsWith(prefix, StringComparison.Ordinal) || !trimmed.EndsWith(";", StringComparison.Ordinal))
    {
        return null;
    }

    return trimmed[prefix.Length..^1].Trim();
}

static string Rel(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (dir.GetFiles("*.sln").Length > 0)
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException($"Could not find a .sln above '{start}'. Pass the repo root explicitly.");
}
