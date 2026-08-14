namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// SC5 / MON-08 open-core boundary leak-guard.
///
/// The dependency direction is one-way only: Enterprise/billing/quota-enforcement may depend on
/// the OSS engine, never the reverse. These reflection tests fail the build if the OSS
/// <c>Muonroi.Pdf</c> assembly ever acquires a reference to a billing- or quota-enforcement
/// assembly, guarding the inviolable open-core boundary against future regressions (T-17-30).
///
/// Test 3 is a deliberate counter-assertion: it proves the quota seam genuinely lives on the
/// Enterprise side (<c>Muonroi.Pdf.Enterprise</c> DOES reference <c>Muonroi.Quota.Abstractions</c>),
/// so the absence assertions in Test 1/2 are meaningful rather than vacuous.
/// </summary>
public sealed class OssBoundaryBillingLeakTests
{
    /// <summary>The OSS engine assembly, resolved via a known public type.</summary>
    private static readonly Assembly OssPdfAssembly =
        typeof(PdfServiceCollectionExtensions).Assembly;

    /// <summary>The Enterprise assembly, resolved via a known public type.</summary>
    private static readonly Assembly EnterprisePdfAssembly =
        typeof(IFeatureGate).Assembly;

    private const string BillingAbstractions = "Muonroi.Billing.Abstractions";
    private const string QuotaAbstractions = "Muonroi.Quota.Abstractions";
    private const string AspNetCore = "Muonroi.AspNetCore";

    private static IReadOnlyList<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

    /// <summary>
    /// Test 1: the OSS <c>Muonroi.Pdf</c> assembly must NOT reference
    /// <c>Muonroi.Billing.Abstractions</c>. Billing is an Enterprise/control-plane concern.
    /// </summary>
    [Fact]
    public void OssPdf_does_not_reference_Billing_Abstractions()
    {
        IReadOnlyList<string> referenced = ReferencedAssemblyNames(OssPdfAssembly);

        Assert.DoesNotContain(BillingAbstractions, referenced);
    }

    /// <summary>
    /// Test 2: the OSS <c>Muonroi.Pdf</c> assembly must NOT reference any quota-enforcement
    /// assembly — neither <c>Muonroi.Quota.Abstractions</c> nor <c>Muonroi.AspNetCore</c>.
    /// Quota enforcement (HTTP 429) lives at the API/Enterprise layer, never in the OSS engine (SC5).
    /// </summary>
    [Fact]
    public void OssPdf_does_not_reference_Quota_or_AspNetCore_enforcement()
    {
        IReadOnlyList<string> referenced = ReferencedAssemblyNames(OssPdfAssembly);

        string[] forbidden = [QuotaAbstractions, AspNetCore];

        foreach (string forbiddenName in forbidden)
        {
            Assert.DoesNotContain(forbiddenName, referenced);
        }
    }

    /// <summary>
    /// Test 3 (counter-assertion): <c>Muonroi.Pdf.Enterprise</c> IS allowed to — and does —
    /// reference <c>Muonroi.Quota.Abstractions</c> (the <c>EnterprisePdfServiceWrapper</c> metering
    /// dependency). This proves the quota seam lives on the Enterprise side, making Test 1/2
    /// meaningful rather than vacuous.
    /// </summary>
    [Fact]
    public void EnterprisePdf_does_reference_Quota_Abstractions()
    {
        IReadOnlyList<string> referenced = ReferencedAssemblyNames(EnterprisePdfAssembly);

        Assert.Contains(QuotaAbstractions, referenced);
    }
}
