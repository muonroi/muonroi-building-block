using ModelContextProtocol.Server;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleGen.Mcp.Infrastructure;

namespace Muonroi.RuleGen.Mcp.Resources;

[McpServerResourceType]
public sealed class DeveloperResourceHandler(IMJsonSerializeService jsonService)
{
    [McpServerResource(UriTemplate = "muonroi://ecosystem/rules", Name = "Muonroi Ecosystem Rules")]
    public string GetRules()
    {
        return jsonService.Serialize(EcosystemCatalog.Rules);
    }

    [McpServerResource(UriTemplate = "muonroi://ecosystem/oss-boundary", Name = "Muonroi OSS Boundary")]
    public string GetOssBoundary()
    {
        OssBoundaryCatalog catalog = OssBoundaryCatalog.Load(WorkspaceLocator.GetRepoRoot());
        return jsonService.Serialize(new
        {
            oss = catalog.OssPackages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            commercial = catalog.CommercialPackages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    [McpServerResource(UriTemplate = "muonroi://ecosystem/tooling", Name = "Muonroi Tooling Reference")]
    public string GetToolingReference()
    {
        return jsonService.Serialize(new
        {
            ruleGen = new[]
            {
                "extract",
                "verify",
                "register",
                "generate-tests",
                "merge",
                "split",
                "watch",
                "feel translate",
                "runtime ruleset json"
            },
            decisionTableGen = new[]
            {
                "import-excel",
                "validate",
                "export-json",
                "export-dmn"
            },
            exclusions = new[]
            {
                "Muonroi.RuleGen.VisualStudio is IDE-only and not exposed via MCP.",
                "MockLicenseServer is dev/test-only and not exposed via MCP."
            }
        });
    }

    [McpServerResource(UriTemplate = "muonroi://ecosystem/patterns", Name = "Muonroi Code Patterns")]
    public string GetPatterns()
    {
        return jsonService.Serialize(new
        {
            primaryConstructorDi = "Use primary constructors for services and rules.",
            logging = "Use IMLog<T> and IMLogContext instead of ILogger<T> and LogContext.",
            time = "Use IMDateTimeService for time access.",
            serialization = "Use IMJsonSerializeService for JSON except explicitly exempt static helpers.",
            dbContext = "Derive from MDbContext and pass IMediator, ILicenseGuard?, IMLog<MDbContext>?, IMDateTimeService?."
        });
    }
}
