namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Severity level for an <see cref="IMEcosystemDiagnostic"/> check result.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational — no action required.</summary>
    Info = 0,

    /// <summary>Warning — app continues but configuration may be sub-optimal.</summary>
    Warning = 1,

    /// <summary>Critical — app startup is blocked until the issue is resolved.</summary>
    Critical = 2
}
