namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Contract for a single Muonroi ecosystem startup diagnostic check.
/// Implementations are discovered and executed by <c>UseMuonroiDiagnostics()</c> during app startup.
/// </summary>
public interface IMEcosystemDiagnostic
{
    /// <summary>Gets the human-readable name of this diagnostic.</summary>
    string Name { get; }

    /// <summary>Gets the severity of a failure for this diagnostic.</summary>
    DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Runs the diagnostic check against the application's service provider.
    /// </summary>
    /// <param name="sp">The root <see cref="IServiceProvider"/> for the application.</param>
    /// <param name="ct">Cancellation token for async operations.</param>
    /// <returns>A <see cref="DiagnosticResult"/> indicating pass/fail and diagnostic details.</returns>
    Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct);
}
