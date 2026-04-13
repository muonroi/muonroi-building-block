using System.ComponentModel;
using ModelContextProtocol.Server;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleGen.Mcp.Infrastructure;

namespace Muonroi.RuleGen.Mcp.Tools.Compliance;

[McpServerToolType]
public sealed class CheckMbbViolationsTool(
    ComplianceScanner scanner,
    IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_compliance_check")]
    public async Task<string> ExecuteAsync(
        [Description("File or directory paths to scan")] string[] paths,
        string includePattern = "**/*.cs",
        string[]? excludePatterns = null,
        CancellationToken ct = default)
    {
        var result = await scanner.ScanAsync(paths, includePattern, excludePatterns ?? [], ct);
        return jsonService.Serialize(result);
    }
}

[McpServerToolType]
public sealed class SuggestEcosystemWrapperTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_compliance_suggest_wrapper")]
    public string Execute(string code, string? violationType = null)
    {
        return jsonService.Serialize(EcosystemCatalog.Suggest(code, violationType));
    }
}

[McpServerToolType]
public sealed class CheckOssBoundaryTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_compliance_check_oss_boundary")]
    public string Execute(string? workspaceRoot = null)
    {
        string root = string.IsNullOrWhiteSpace(workspaceRoot) ? WorkspaceLocator.GetWorkspaceRoot() : Path.GetFullPath(workspaceRoot);
        string repoRoot = Directory.Exists(Path.Combine(root, "muonroi-building-block"))
            ? Path.Combine(root, "muonroi-building-block")
            : root;

        OssBoundaryCatalog catalog = OssBoundaryCatalog.Load(repoRoot);
        return jsonService.Serialize(catalog.Check(root));
    }
}
