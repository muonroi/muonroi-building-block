using System.Linq;
using Muonroi.Pdf.Abstractions.Policy;

namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfPolicyException : PdfException
{
    public IReadOnlyList<PolicyViolation> Violations { get; }

    public PdfPolicyException(IReadOnlyList<PolicyViolation> violations)
        : base(
            violations[0].RuleId,
            string.Join(", ", violations.Select(v => v.RuleId)),
            $"PDF policy validation failed with {violations.Count} violation(s): {violations[0].RuleId}")
    {
        Violations = violations;
    }
}
