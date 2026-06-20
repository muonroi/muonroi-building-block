using System.Linq;
using Muonroi.Pdf.Abstractions.Policy;

namespace Muonroi.Pdf.Abstractions.Exceptions;

/// <summary>
/// Thrown when one or more CSS policy rules are violated during the policy gate check.
/// All violations detected in a single pass are collected and surfaced together.
/// </summary>
/// <param name="violations">
/// Non-empty list of policy violations found during validation. The first violation's
/// <see cref="PolicyViolation.RuleId"/> is used as the primary <see cref="PdfException.RuleId"/>.
/// </param>
public sealed class PdfPolicyException(IReadOnlyList<PolicyViolation> violations) : PdfException(
        violations[0].RuleId,
        string.Join(", ", violations.Select(v => v.RuleId)),
        $"PDF policy validation failed with {violations.Count} violation(s): {violations[0].RuleId}")
{
    /// <summary>All policy violations detected during the policy gate check, in evaluation order.</summary>
    public IReadOnlyList<PolicyViolation> Violations { get; } = violations;
}
