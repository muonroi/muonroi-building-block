namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// The result of a single <see cref="IMEcosystemDiagnostic"/> check.
/// </summary>
/// <param name="Passed">Whether the diagnostic check passed.</param>
/// <param name="Message">Human-readable description of the check outcome.</param>
/// <param name="FixSuggestion">Optional actionable suggestion when the check fails (e.g., "Call services.AddTenantContext()").</param>
/// <param name="Details">Optional dictionary of additional diagnostic context values.</param>
public sealed record DiagnosticResult(
    bool Passed,
    string Message,
    string? FixSuggestion = null,
    Dictionary<string, object>? Details = null)
{
    /// <summary>Creates a passing result with the given message.</summary>
    public static DiagnosticResult Pass(string message) =>
        new(true, message);

    /// <summary>Creates a failing result with the given message and optional fix suggestion.</summary>
    public static DiagnosticResult Fail(string message, string? fixSuggestion = null, Dictionary<string, object>? details = null) =>
        new(false, message, fixSuggestion, details);
}
