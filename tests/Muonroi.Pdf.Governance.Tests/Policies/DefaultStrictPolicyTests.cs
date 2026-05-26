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
}
