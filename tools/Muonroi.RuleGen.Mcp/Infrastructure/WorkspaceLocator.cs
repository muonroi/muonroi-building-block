namespace Muonroi.RuleGen.Mcp.Infrastructure;

internal static class WorkspaceLocator
{
    public static string GetRepoRoot()
    {
        string? current = Environment.GetEnvironmentVariable("MUONROI_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            return current;
        }

        string probe = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(probe))
        {
            if (File.Exists(Path.Combine(probe, "Muonroi.BuildingBlock.sln")) ||
                Directory.Exists(Path.Combine(probe, "tools", "Muonroi.RuleGen")))
            {
                return probe;
            }

            string? parent = Directory.GetParent(probe)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, probe, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            probe = parent;
        }

        throw new DirectoryNotFoundException("Cannot locate muonroi-building-block repository root.");
    }

    public static string GetWorkspaceRoot()
    {
        string? explicitRoot = Environment.GetEnvironmentVariable("MUONROI_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(explicitRoot) && Directory.Exists(explicitRoot))
        {
            return explicitRoot;
        }

        return Directory.GetParent(GetRepoRoot())?.FullName
            ?? throw new DirectoryNotFoundException("Cannot derive workspace root from repository root.");
    }

    public static string FindNearestProjectFile(string path)
    {
        string current = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
        while (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            string[] matches = Directory.GetFiles(current, "*.csproj", SearchOption.TopDirectoryOnly);
            if (matches.Length > 0)
            {
                return matches[0];
            }

            string? parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        throw new FileNotFoundException($"Cannot locate a .csproj file for path '{path}'.");
    }
}
