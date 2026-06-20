using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Muonroi.AspNetCore.Diagnostics;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering and running the Muonroi ecosystem startup diagnostics.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0003",
    Justification = "Startup diagnostics runner obtains a logger from ILoggerFactory (always available) at app startup, before/independent of whether Muonroi.Logging is registered. It also logs at Critical level, which IMLog does not expose. Uses the Microsoft ILogger by design.")]
public static class DiagnosticsExtensions
{
    // Marker type used for ILogger category
    private sealed class MuonroiDiagnosticsMarker { }

    /// <summary>
    /// Registers all 6 built-in <see cref="IMEcosystemDiagnostic"/> implementations into the DI container.
    /// Call this from <c>Program.cs</c> before <c>builder.Build()</c>.
    /// </summary>
    /// <remarks>
    /// Built-in diagnostics registered:
    /// <list type="bullet">
    ///   <item><description><see cref="DependencyGraphDiagnostic"/> (Critical)</description></item>
    ///   <item><description><see cref="ConfigurationDiagnostic"/> (Critical)</description></item>
    ///   <item><description><see cref="ConnectivityDiagnostic"/> (Critical)</description></item>
    ///   <item><description><see cref="EcosystemRegistryDiagnostic"/> (Warning)</description></item>
    ///   <item><description><see cref="LicenseStatusDiagnostic"/> (Warning)</description></item>
    ///   <item><description><see cref="TenantConfigDiagnostic"/> (Warning)</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddMuonroiDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<IMEcosystemDiagnostic, DependencyGraphDiagnostic>();
        services.AddSingleton<IMEcosystemDiagnostic, ConfigurationDiagnostic>();
        services.AddSingleton<IMEcosystemDiagnostic, ConnectivityDiagnostic>();
        services.AddSingleton<IMEcosystemDiagnostic, EcosystemRegistryDiagnostic>();
        services.AddSingleton<IMEcosystemDiagnostic, LicenseStatusDiagnostic>();
        services.AddSingleton<IMEcosystemDiagnostic, TenantConfigDiagnostic>();

        return services;
    }

    /// <summary>
    /// Runs all registered <see cref="IMEcosystemDiagnostic"/> checks during application startup.
    /// </summary>
    /// <remarks>
    /// Behavior:
    /// <list type="bullet">
    ///   <item><description>Warning failures — logged and app continues.</description></item>
    ///   <item><description>Critical failures — logged with fix suggestion and <see cref="InvalidOperationException"/> thrown to block startup.</description></item>
    ///   <item><description>All pass — logs a summary at Info level.</description></item>
    /// </list>
    /// Call this from <c>Program.cs</c> after <c>app.Build()</c>, before <c>app.Run()</c>.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if any Critical diagnostic fails.</exception>
    public static IApplicationBuilder UseMuonroiDiagnostics(
        this IApplicationBuilder app,
        CancellationToken cancellationToken = default)
    {
        IServiceProvider sp = app.ApplicationServices;
        ILogger logger = sp.GetRequiredService<ILoggerFactory>()
            .CreateLogger<MuonroiDiagnosticsMarker>();

        IEnumerable<IMEcosystemDiagnostic> diagnostics = sp.GetServices<IMEcosystemDiagnostic>();

        List<IMEcosystemDiagnostic> diagnosticList = [.. diagnostics];
        if (diagnosticList.Count == 0)
        {
            logger.LogInformation("[MUONROI:DIAG] No diagnostics registered. Call AddMuonroiDiagnostics() to enable startup checks.");
            return app;
        }

        // Run diagnostics synchronously at startup (blocking is intentional for startup-time validation)
        RunDiagnosticsAsync(sp, logger, diagnosticList, cancellationToken)
            .GetAwaiter()
            .GetResult();

        return app;
    }

    private static async Task RunDiagnosticsAsync(
        IServiceProvider sp,
        ILogger logger,
        List<IMEcosystemDiagnostic> diagnostics,
        CancellationToken ct)
    {
        int passed = 0;
        int warned = 0;
        var criticalFailures = new List<(string Name, DiagnosticResult Result)>();

        foreach (IMEcosystemDiagnostic diagnostic in diagnostics)
        {
            DiagnosticResult result;
            try
            {
                result = await diagnostic.CheckAsync(sp, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = DiagnosticResult.Fail(
                    $"Diagnostic '{diagnostic.Name}' threw an unhandled exception: {ex.Message}",
                    fixSuggestion: "Investigate the exception in the diagnostic implementation.",
                    details: new Dictionary<string, object>
                    {
                        ["exceptionType"] = ex.GetType().Name,
                        ["exceptionMessage"] = ex.Message
                    });
            }

            if (result.Passed)
            {
                passed++;
                logger.LogDebug("[MUONROI:DIAG] PASS ── {DiagnosticName}: {Message}",
                    diagnostic.Name, result.Message);
                continue;
            }

            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Info:
                    passed++;
                    logger.LogInformation("[MUONROI:DIAG] INFO ── {DiagnosticName}: {Message}  →  {FixSuggestion}",
                        diagnostic.Name, result.Message, result.FixSuggestion ?? "No fix suggestion.");
                    break;

                case DiagnosticSeverity.Warning:
                    warned++;
                    logger.LogWarning("[MUONROI:DIAG] WARNING ── {DiagnosticName}: {Message}  →  {FixSuggestion}",
                        diagnostic.Name, result.Message, result.FixSuggestion ?? "No fix suggestion.");
                    break;

                case DiagnosticSeverity.Critical:
                    criticalFailures.Add((diagnostic.Name, result));
                    logger.LogCritical(
                        "[MUONROI:DIAG] CRITICAL ── {DiagnosticName}{NewLine}  {Message}{NewLine}  Fix: {FixSuggestion}",
                        diagnostic.Name,
                        Environment.NewLine,
                        result.Message,
                        Environment.NewLine,
                        result.FixSuggestion ?? "No fix suggestion provided.");
                    break;
            }
        }

        if (criticalFailures.Count > 0)
        {
            string summary = string.Join(Environment.NewLine,
                criticalFailures.Select(f =>
                    $"  [{f.Name}] {f.Result.Message}" +
                    (f.Result.FixSuggestion is not null ? $"  →  {f.Result.FixSuggestion}" : string.Empty)));

            throw new MInternalException(
                $"Muonroi startup diagnostics failed with {criticalFailures.Count} critical error(s):" +
                Environment.NewLine + summary,
                "STARTUP_DIAGNOSTICS_FAILED");
        }

        logger.LogInformation(
            "[MUONROI:DIAG] All {Total} diagnostic(s) passed. {Warnings} warning(s) noted.",
            diagnostics.Count, warned);
    }
}
