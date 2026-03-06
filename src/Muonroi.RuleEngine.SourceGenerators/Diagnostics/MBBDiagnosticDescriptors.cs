using Microsoft.CodeAnalysis;

namespace Muonroi.RuleEngine.SourceGenerators.Diagnostics;

internal static class MBBDiagnosticDescriptors
{
    private const string Category = "Muonroi.Governance";

    public static readonly DiagnosticDescriptor MBB001ForbiddenDateTimeNow = new(
        id: "MBB001",
        title: "Forbidden DateTime.Now/UtcNow",
        messageFormat: "Use IMDateTimeService instead of '{0}' in namespace '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MBB002ForbiddenJsonSerializer = new(
        id: "MBB002",
        title: "Forbidden direct JsonSerializer usage",
        messageFormat: "Use IMJsonSerializeService instead of direct JsonSerializer member '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MBB003ForbiddenDbContextInheritance = new(
        id: "MBB003",
        title: "Forbidden DbContext inheritance",
        messageFormat: "Type '{0}' must inherit MDbContext (or be placed in infrastructure namespace)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MBB004ForbiddenAsyncLocal = new(
        id: "MBB004",
        title: "Forbidden AsyncLocal usage outside context package",
        messageFormat: "Use ISystemExecutionContextAccessor instead of AsyncLocal in namespace '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MBB005AbstractionInfraDependency = new(
        id: "MBB005",
        title: "Abstractions assembly references infrastructure dependency",
        messageFormat: "Abstractions assembly '{0}' should not reference infrastructure dependency '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor MBB006MissingTierGuard = new(
        id: "MBB006",
        title: "Missing startup tier guard",
        messageFormat: "Registration method '{0}' should call EnsureFeatureOrThrow before '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MBB007ForbiddenSerilogContext = new(
        id: "MBB007",
        title: "Forbidden direct Serilog LogContext usage",
        messageFormat: "Use IMLogContext.PushProperty() instead of Serilog.Context.LogContext directly",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
