using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleGen.Mcp.Models;

namespace Muonroi.RuleGen.Mcp.Tools.Scaffold;

internal static class ScaffoldTemplates
{
    public static string BuildRuleClass(string ruleCode, string contextType, string @namespace, int order, string hookPoint, string ruleType)
    {
        return $$"""
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;

namespace {{@namespace}};

public sealed class {{ToIdentifier(ruleCode)}}RuleSource(
    ISystemExecutionContextAccessor contextAccessor,
    IMLog<{{ToIdentifier(ruleCode)}}RuleSource> log)
{
    [MExtractAsRule("{{ruleCode}}", Order = {{order}}, HookPoint = HookPoint.{{hookPoint}})]
    public async Task<RuleResult> EvaluateAsync({{contextType}} ctx, FactBag facts, CancellationToken ct = default)
    {
        string? tenantId = contextAccessor.Get()?.TenantId;
        log.Info("Evaluating {{ruleCode}} for tenant {TenantId}", tenantId);

        // TODO: implement {{ruleType}} rule logic.
        await Task.CompletedTask;
        return RuleResult.Passed();
    }
}
""";
    }

    public static string BuildRepository(string entityName, string dbContextName, string @namespace, bool generateInterface)
    {
        string repositoryName = $"{entityName}Repository";
        string interfaceName = $"I{repositoryName}";
        StringBuilder sb = new();
        sb.Append($$"""
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Governance;

namespace {{@namespace}};

{{(generateInterface ? $"public interface {interfaceName} : IMRepository<{entityName}>\n{{\n}}\n\n" : string.Empty)}}public sealed class {{repositoryName}}(
    {{dbContextName}} dbContext,
    IAuthenticateInfoContext authenticateInfoContext,
    ILicenseGuard licenseGuard,
    IMDateTimeService dateTimeService)
    : MRepository<{{entityName}}>(dbContext, authenticateInfoContext, licenseGuard, dateTimeService){{(generateInterface ? $", {interfaceName}" : string.Empty)}}
{
}
"""
);
        return sb.ToString();
    }

    public static string BuildDbContext(string contextName, string @namespace, IReadOnlyList<string> entityNames)
    {
        string dbSets = string.Join("\n", entityNames.Select(name => $"    public DbSet<{name}> {name}s => Set<{name}>();"));
        return $$"""
using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Governance;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator;

namespace {{@namespace}};

public sealed class {{contextName}}(
    DbContextOptions options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null,
    IMDateTimeService? dateTimeService = null)
    : MDbContext(options, mediator, licenseGuard, logger, dateTimeService)
{
{{dbSets}}
}
""";
    }

    public static string BuildService(string serviceName, string @namespace, string? interfaceName, bool includeDateTime, bool includeJson, bool includeLogging, bool includeContext)
    {
        string contract = string.IsNullOrWhiteSpace(interfaceName) ? $"I{serviceName}" : interfaceName;
        List<string> dependencies = [];
        if (includeDateTime) dependencies.Add("IMDateTimeService dateTimeService");
        if (includeJson) dependencies.Add("IMJsonSerializeService jsonSerializeService");
        if (includeLogging) dependencies.Add($"IMLog<{serviceName}> log");
        if (includeContext) dependencies.Add("ISystemExecutionContextAccessor contextAccessor");
        string ctor = dependencies.Count == 0 ? string.Empty : $"({string.Join(", ", dependencies)})";

        return $$"""
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging.Abstractions;

namespace {{@namespace}};

public interface {{contract}}
{
    Task ExecuteAsync(CancellationToken ct = default);
}

public sealed class {{serviceName}}{{ctor}} : {{contract}}
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        {{(includeLogging ? "log.Info(\"Executing service {ServiceName}\", nameof(" + serviceName + "));" : "")}}
        {{(includeContext ? "string? tenantId = contextAccessor.Get()?.TenantId;" : string.Empty)}}
        {{(includeDateTime ? "DateTime utcNow = dateTimeService.UtcNow();" : string.Empty)}}
        await Task.CompletedTask;
    }
}
""";
    }

    private static string ToIdentifier(string value)
    {
        char[] buffer = [.. value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')];
        string normalized = new string(buffer).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Generated";
        }

        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }
}

[McpServerToolType]
public sealed class ScaffoldRuleClassTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_scaffold_rule_class")]
    public string Execute(string ruleCode, string contextType, string @namespace, int order = 0, string hookPoint = "BeforeRule", string ruleType = "Validation")
    {
        string fileName = $"{ruleCode.Replace("-", "_", StringComparison.Ordinal)}RuleSource.cs";
        return jsonService.Serialize(new ScaffoldResult(fileName, ScaffoldTemplates.BuildRuleClass(ruleCode, contextType, @namespace, order, hookPoint, ruleType)));
    }
}

[McpServerToolType]
public sealed class ScaffoldRepositoryTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_scaffold_repository")]
    public string Execute(string entityName, string dbContextName, string @namespace, bool generateInterface = true)
    {
        string fileName = $"{entityName}Repository.cs";
        string code = ScaffoldTemplates.BuildRepository(entityName, dbContextName, @namespace, generateInterface);
        return jsonService.Serialize(new ScaffoldResult(fileName, code));
    }
}

[McpServerToolType]
public sealed class ScaffoldDbContextTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_scaffold_dbcontext")]
    public string Execute(string contextName, string @namespace, string[]? entityNames = null)
    {
        string fileName = $"{contextName}.cs";
        string code = ScaffoldTemplates.BuildDbContext(contextName, @namespace, entityNames ?? []);
        return jsonService.Serialize(new ScaffoldResult(fileName, code));
    }
}

[McpServerToolType]
public sealed class ScaffoldServiceTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_scaffold_service")]
    public string Execute(
        string serviceName,
        string @namespace,
        string? interfaceName = null,
        bool includeDateTime = true,
        bool includeJson = false,
        bool includeLogging = true,
        bool includeContext = true)
    {
        string fileName = $"{serviceName}.cs";
        string code = ScaffoldTemplates.BuildService(serviceName, @namespace, interfaceName, includeDateTime, includeJson, includeLogging, includeContext);
        return jsonService.Serialize(new ScaffoldResult(fileName, code));
    }
}
