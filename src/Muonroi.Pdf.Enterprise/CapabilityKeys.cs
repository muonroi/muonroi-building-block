namespace Muonroi.Pdf.Enterprise;

/// <summary>
/// Constant capability key strings for Muonroi.Pdf Enterprise features.
/// Follows the <c>&lt;domain&gt;.&lt;feature&gt;</c> naming convention used across the
/// Muonroi ecosystem (e.g., <c>core.runtime</c>, <c>auth.rbac_plus</c>).
/// </summary>
public static class CapabilityKeys
{
    /// <summary>Template designer / visual editor feature.</summary>
    public const string PdfDesigner = "pdf.designer";

    /// <summary>Template registry client feature (LookupAsync / ResolveAsync / SubscribeAsync).</summary>
    public const string PdfRegistry = "pdf.registry";

    /// <summary>Canary quality-regression scorer feature (SSIM-based visual diff).</summary>
    public const string PdfCanary = "pdf.canary";
}
