namespace Muonroi.Pdf.Governance.Tests.Policies;

public sealed class DefaultStrictPolicyTests
{
    private static async Task<IStyledDocument> ParseAndCascadeAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);

        var cascade = new AngleSharpCascadeEngine();
        return await cascade.CascadeAsync(parsed, null).ConfigureAwait(false);
    }

    [Fact]
    public async Task DefaultStrictPolicy_BorderCollapseCollapse_EmitsPolicyViolation()
    {
        const string html = "<html><head><style>table { border-collapse: collapse; }</style></head><body><table><tr><td>cell</td></tr></table></body></html>";

        IStyledDocument doc = await ParseAndCascadeAsync(html).ConfigureAwait(false);
        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync((IPdfDocumentContext)doc).ConfigureAwait(false);

        result.Accepted.Should().BeFalse();
        result.Violations.Should().Contain(v =>
            v.RuleId == "forbidden.border-collapse.collapse" &&
            v.SuggestedAlternative != null &&
            v.SuggestedAlternative.Contains("border-collapse:separate"));
    }

    // C4 fail-loud: unsupported visual properties must produce a loud violation, not silent drop.

    [Fact]
    public async Task DefaultStrictPolicy_Filter_EmitsUnsupportedViolation()
    {
        const string html = "<html><head><style>div { filter: blur(2px); }</style></head><body><div>x</div></body></html>";

        IStyledDocument doc = await ParseAndCascadeAsync(html).ConfigureAwait(false);
        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync((IPdfDocumentContext)doc).ConfigureAwait(false);

        result.Accepted.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleId == "unsupported.filter");
    }

    [Fact]
    public async Task DefaultStrictPolicy_MixBlendMode_EmitsUnsupportedViolation()
    {
        const string html = "<html><head><style>div { mix-blend-mode: multiply; }</style></head><body><div>x</div></body></html>";

        IStyledDocument doc = await ParseAndCascadeAsync(html).ConfigureAwait(false);
        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync((IPdfDocumentContext)doc).ConfigureAwait(false);

        result.Accepted.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleId == "unsupported.mix-blend-mode");
    }

    [Fact]
    public async Task DefaultStrictPolicy_NoUnsupportedVisualProps_DoesNotEmitUnsupportedViolation()
    {
        const string html = "<html><head><style>div { color: #333; padding: 4px; }</style></head><body><div>x</div></body></html>";

        IStyledDocument doc = await ParseAndCascadeAsync(html).ConfigureAwait(false);
        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync((IPdfDocumentContext)doc).ConfigureAwait(false);

        result.Violations.Should().NotContain(v => v.RuleId.StartsWith("unsupported."));
    }
}
