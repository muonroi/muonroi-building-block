namespace Muonroi.Pdf.Abstractions.Policy;

/// <summary>
/// Outcome of an <see cref="IPdfCssPolicy"/> validation pass.
/// </summary>
public sealed record PolicyValidationResult(
    bool Accepted,
    IReadOnlyList<PolicyViolation> Violations)
{
    /// <summary>Shared empty result for a successful validation.</summary>
    public static readonly PolicyValidationResult Ok = new(true, Array.Empty<PolicyViolation>());

    /// <summary>Creates a hard-fail result with one violation.</summary>
    public static PolicyValidationResult Fail(PolicyViolation violation) =>
        new(false, new[] { violation });
}

/// <summary>
/// A single policy violation. Hard-fail by default; soft-degrade behavior is policy-specific.
/// </summary>
/// <param name="RuleId">Stable identifier (e.g. <c>limit.max-dom-depth</c>, <c>forbidden.tag.script</c>).</param>
/// <param name="Message">Human-readable description suitable for log lines (no template content).</param>
/// <param name="Severity">Violation severity.</param>
/// <param name="PropertyName">CSS property name that triggered the violation, if applicable.</param>
/// <param name="RejectedValue">The value that was rejected, if applicable.</param>
/// <param name="CssSelector">CSS selector context where the violation occurred, if applicable.</param>
/// <param name="SuggestedAlternative">Replacement value or approach the caller may use instead, if applicable.</param>
public sealed record PolicyViolation(
    string RuleId,
    string Message,
    PolicySeverity Severity = PolicySeverity.Error,
    string? PropertyName = null,
    string? RejectedValue = null,
    string? CssSelector = null,
    string? SuggestedAlternative = null);

/// <summary>
/// Severity classification for a policy violation.
/// </summary>
public enum PolicySeverity
{
    /// <summary>Hard-fail: rendering aborts.</summary>
    Error = 0,

    /// <summary>Reported but rendering continues (e.g. soft-degrade behaviors).</summary>
    Warning = 1
}
