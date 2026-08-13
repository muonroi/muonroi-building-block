using Microsoft.CodeAnalysis;

namespace Muonroi.CodeStandards.Diagnostics;

internal static class MstdDiagnosticDescriptors
{
    private const string Category = "Muonroi.CodeStandards";

    public static readonly DiagnosticDescriptor Mstd0001ForbiddenThrow = new(
        id: "MSTD0001",
        title: "Throw must go through MGuard or an MException-derived type",
        messageFormat: "Throw via MGuard or an MException-derived type; raw '{0}' is forbidden in namespace '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All Muonroi code must validate and throw via MGuard or throw an MException-derived type. Raw framework exceptions (e.g. ArgumentNullException) are forbidden.");

    public static readonly DiagnosticDescriptor Mstd0002NullForgiving = new(
        id: "MSTD0002",
        title: "Null-forgiving operator '!' is forbidden",
        messageFormat: "Null-forgiving operator '!' is forbidden in namespace '{0}'; validate with MGuard.NotNull instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The null-forgiving operator '!' suppresses null-safety. Validate with MGuard.NotNull. Suppress intentionally with '#pragma warning disable MSTD0002' only when truly required.");

    public static readonly DiagnosticDescriptor Mstd0003LoggingViaMLog = new(
        id: "MSTD0003",
        title: "Logging must go through IMLog",
        messageFormat: "Logging must go through IMLog (Muonroi.Logging.Abstractions); '{0}' is forbidden in namespace '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All Muonroi code must log via IMLog/IMLog<T>. Direct Console/Debug/Trace/Serilog logging and raw ILogger usage are forbidden — inject IMLog<T> instead. Suppress with '#pragma warning disable MSTD0003' or [SuppressMessage] only for pre-DI bootstrap code where IMLog is genuinely unavailable.");

    public static readonly DiagnosticDescriptor Mstd0004DirectMGuardBypass = new(
        id: "MSTD0004",
        title: "Standard Muonroi exceptions must be thrown via MGuard",
        messageFormat: "Manually throwing '{0}' is forbidden, use the corresponding MGuard method instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Do not manually 'throw new MInternalException', 'MConfigurationException', 'MArgumentException', or 'MNotFoundException'. Use the MGuard utility methods instead to ensure consistent exception classification and logging.");
}
