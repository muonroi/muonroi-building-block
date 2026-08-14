namespace Muonroi.Pdf.Tests.Policy;

/// <summary>
/// Tests for DefaultStrictPolicy href scheme gate (FIDELITY-12).
/// </summary>
public sealed class PolicyTests
{
    private static async Task<IPdfDocumentContext> ParseAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var cascade = new AngleSharpCascadeEngine();
        IStyledDocument doc = await cascade.CascadeAsync(parsed, null).ConfigureAwait(false);
        return (IPdfDocumentContext)doc;
    }

    [Fact]
    public async Task CheckCssFeatures_JavascriptHref_ProducesPolicyViolation()
    {
        const string html = "<html><body><a href=\"javascript:alert(1)\">x</a></body></html>";
        var context = await ParseAsync(html);

        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Accepted.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.link.scheme");
    }

    [Fact]
    public async Task CheckCssFeatures_FileHref_ProducesPolicyViolation()
    {
        const string html = "<html><body><a href=\"file:///etc/passwd\">x</a></body></html>";
        var context = await ParseAsync(html);

        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Accepted.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.link.scheme");
    }

    [Fact]
    public async Task CheckCssFeatures_HttpsHref_NoPolicyViolation()
    {
        const string html = "<html><body><a href=\"https://example.com\">x</a></body></html>";
        var context = await ParseAsync(html);

        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.link.scheme");
    }
}
