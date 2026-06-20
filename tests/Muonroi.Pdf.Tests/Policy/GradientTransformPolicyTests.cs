using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;
using Muonroi.Pdf.Governance.Policies;

namespace Muonroi.Pdf.Tests.Policy;

/// <summary>
/// Phase 15 (Plan 01): the legacy-print policy now allows the full CSS 2D affine transform set and
/// radial-gradient backgrounds, while still rejecting conic/repeating gradients, perspective(), and
/// other unknown transform functions.
/// </summary>
public sealed class GradientTransformPolicyTests
{
    private static async Task<IPdfDocumentContext> ParseAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var cascade = new AngleSharpCascadeEngine();
        IStyledDocument doc = await cascade.CascadeAsync(parsed, null).ConfigureAwait(false);
        return (IPdfDocumentContext)doc;
    }

    private static async Task<PolicyValidationResult> ValidateAsync(string css)
    {
        string html = $"<html><head><style>div{{{css}}}</style></head><body><div>x</div></body></html>";
        IPdfDocumentContext context = await ParseAsync(html);
        return await new LegacyPrintPolicy().ValidateAsync(context);
    }

    // Validates via the inline style="" attribute path (bypasses AngleSharp computed-style
    // normalization, which strips unknown CSS property values before the policy gate sees them).
    private static async Task<PolicyValidationResult> ValidateInlineAsync(string inlineCss)
    {
        string html = $"<html><body><div style=\"{inlineCss}\">x</div></body></html>";
        IPdfDocumentContext context = await ParseAsync(html);
        return await new LegacyPrintPolicy().ValidateAsync(context);
    }

    // ── Gradient gate tests ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LinearGradient_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("background:linear-gradient(90deg,#0c6b6b,#fff);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.background.gradient",
            because: "linear-gradient is supported via PDF axial shading");
    }

    /// <summary>Phase 15: radial-gradient is now supported (D-06). Formerly rejected.</summary>
    [Fact]
    public async Task RadialGradient_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("background:radial-gradient(#fff,#000);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.background.gradient",
            because: "radial-gradient is supported as of Phase 15");
    }

    [Fact]
    public async Task RepeatingLinearGradient_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("background:repeating-linear-gradient(#fff,#000 10px);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.background.gradient",
            because: "repeating gradients are out of scope");
    }

    [Fact]
    public async Task ConicGradient_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("background:conic-gradient(red,blue);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.background.gradient",
            because: "conic-gradient is not supported");
    }

    [Fact]
    public async Task RepeatingRadialGradient_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("background:repeating-radial-gradient(#fff,#000 10px);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.background.gradient",
            because: "repeating gradients remain out of scope");
    }

    // ── Transform gate tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TransformRotate_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("transform:rotate(45deg);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.transform.geometric",
            because: "rotate() is supported (Phase 14 parity)");
    }

    /// <summary>Phase 15: translate is now allowed (D-01). Formerly rejected.</summary>
    [Fact]
    public async Task TransformTranslate_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("transform:translate(10px,10px);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.transform.geometric",
            because: "translate() is in the affine allowlist (D-01)");
    }

    [Fact]
    public async Task TransformScale_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("transform:scale(0.8);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.transform.geometric",
            because: "scale() is in the affine allowlist (D-01)");
    }

    [Fact]
    public async Task TransformMatrix_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("transform:matrix(1,0,0,1,10,20);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.transform.geometric",
            because: "matrix() is in the affine allowlist (D-01)");
    }

    /// <summary>Phase 15: multi-function chains are now allowed (D-01). Formerly rejected.</summary>
    [Fact]
    public async Task TransformChain_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("transform:rotate(45deg) scale(2);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.transform.geometric",
            because: "multi-function affine chains are allowed (D-01)");
    }

    [Fact]
    public async Task TransformPerspective_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("transform:perspective(100px);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.transform.geometric",
            because: "perspective() is not a 2D affine function and must be rejected");
    }

    [Fact]
    public async Task TransformUnknownFunction_IsRejected()
    {
        // rotate3d() is a valid CSS 3D transform function that AngleSharp preserves in computed
        // style, but it is not in the 2D affine allowlist — the gate must reject it fail-loud (D-02).
        PolicyValidationResult result = await ValidateAsync("transform:rotate3d(1,0,0,45deg);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.transform.geometric",
            because: "rotate3d() is a 3D function, not in the 2D affine allowlist (D-02)");
    }
}
