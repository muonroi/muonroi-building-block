using System.Xml.Linq;
using Muonroi.RuleGen.Mcp.Models;

namespace Muonroi.RuleGen.Mcp.Infrastructure;

internal sealed class OssBoundaryCatalog
{
    private readonly HashSet<string> _ossPackages;
    private readonly HashSet<string> _commercialPackages;

    private OssBoundaryCatalog(HashSet<string> ossPackages, HashSet<string> commercialPackages)
    {
        _ossPackages = ossPackages;
        _commercialPackages = commercialPackages;
    }

    public IReadOnlyCollection<string> OssPackages => _ossPackages;
    public IReadOnlyCollection<string> CommercialPackages => _commercialPackages;

    public static OssBoundaryCatalog Load(string repoRoot)
    {
        string path = Path.Combine(repoRoot, "OSS-BOUNDARY.md");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"OSS boundary document not found: {path}");
        }

        HashSet<string> oss = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> commercial = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? current = null;

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("## OSS Packages", StringComparison.Ordinal))
            {
                current = oss;
                continue;
            }

            if (line.StartsWith("## Commercial Packages", StringComparison.Ordinal))
            {
                current = commercial;
                continue;
            }

            if (current is null || !line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            string packageName = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (!string.IsNullOrWhiteSpace(packageName))
            {
                current.Add(packageName);
            }
        }

        return new OssBoundaryCatalog(oss, commercial);
    }

    public OssBoundaryCheckResult Check(string workspaceRoot)
    {
        List<OssBoundaryViolation> violations = [];
        List<string> notes = [];

        string repoRoot = Directory.Exists(Path.Combine(workspaceRoot, "muonroi-building-block"))
            ? Path.Combine(workspaceRoot, "muonroi-building-block")
            : workspaceRoot;

        foreach (string csproj in Directory.EnumerateFiles(Path.Combine(repoRoot, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            XDocument document = XDocument.Load(csproj);
            string packageName = document.Root?
                .Elements("PropertyGroup")
                .Elements("PackageId")
                .Select(x => x.Value.Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? Path.GetFileNameWithoutExtension(csproj);

            if (!_ossPackages.Contains(packageName))
            {
                continue;
            }

            IEnumerable<string> packageRefs = document.Root?
                .Descendants("PackageReference")
                .Select(x => x.Attribute("Include")?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                ?? [];

            foreach (string reference in packageRefs)
            {
                if (_commercialPackages.Contains(reference))
                {
                    violations.Add(new OssBoundaryViolation(packageName, reference, csproj));
                }
            }

            IEnumerable<string> projectRefs = document.Root?
                .Descendants("ProjectReference")
                .Select(x => x.Attribute("Include")?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Path.GetFileNameWithoutExtension(x!))
                ?? [];

            foreach (string reference in projectRefs)
            {
                if (_commercialPackages.Contains(reference))
                {
                    violations.Add(new OssBoundaryViolation(packageName, reference, csproj));
                }
            }
        }

        if (violations.Count == 0)
        {
            notes.Add("No OSS boundary violations found.");
        }

        return new OssBoundaryCheckResult(violations.Count == 0, violations, notes);
    }
}
