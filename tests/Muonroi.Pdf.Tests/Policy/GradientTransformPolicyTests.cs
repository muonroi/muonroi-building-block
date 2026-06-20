using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;
using Muonroi.Pdf.Governance.Policies;

namespace Muonroi.Pdf.Tests.Policy;

/// <summary>
/// Phase 14 (Groups B + C): the legacy-print policy now allows <c>linear-gradient</c> backgrounds and
/// <c>transform: rotate()</c>, while still rejecting other gradient functions and non-rotate
/// transforms.
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

    [Fact]
    public async Task LinearGradient_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("background:linear-gradient(90deg,#0c6b6b,#fff);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.background.gradient",
            because: "linear-gradient is supported via PDF axial shading");
    }

    [Fact]
    public async Task RadialGradient_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("background:radial-gradient(#fff,#000);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.background.gradient",
            because: "only linear-gradient is supported");
    }

    [Fact]
    public async Task RepeatingLinearGradient_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("background:repeating-linear-gradient(#fff,#000 10px);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.background.gradient",
            because: "repeating gradients are out of scope");
    }

    [Fact]
    public async Task TransformRotate_IsAllowed()
    {
        PolicyValidationResult result = await ValidateAsync("transform:rotate(45deg);");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.transform.geometric",
            because: "rotate() is supported for watermarks");
    }

    [Fact]
    public async Task TransformTranslate_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("transform:translate(10px,10px);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.transform.geometric",
            because: "only rotate() is supported; translate stays rejected");
    }

    [Fact]
    public async Task TransformRotateWithScale_IsRejected()
    {
        PolicyValidationResult result = await ValidateAsync("transform:rotate(45deg) scale(2);");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.transform.geometric",
            because: "multi-function transforms are not supported");
    }
}
