namespace Muonroi.Pdf.Tests.Policy;

/// <summary>
/// Validates LegacyPrintPolicy allow-list and block-list behaviour for Profile v1.
/// Each test asserts against both policies where meaningful to prove DefaultStrictPolicy is unchanged.
/// </summary>
public sealed class LegacyPrintPolicyTests
{
    private static async Task<IPdfDocumentContext> ParseAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var cascade = new AngleSharpCascadeEngine();
        IStyledDocument doc = await cascade.CascadeAsync(parsed, null).ConfigureAwait(false);
        return (IPdfDocumentContext)doc;
    }

    // --- Allow-list: LegacyPrintPolicy allows, DefaultStrictPolicy blocks ---

    [Fact]
    public async Task FloatLeft_PassesLegacy_FailsDefaultStrict()
    {
        const string html = "<html><head><style>div{float:left;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Violations.Should().NotContain(v => v.RuleId == "forbidden.float",
            because: "LegacyPrintPolicy must allow float:left");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.float",
            because: "DefaultStrictPolicy must still block float:left");
    }

    [Fact]
    public async Task PositionAbsolute_PassesLegacy_FailsDefaultStrict()
    {
        const string html = "<html><head><style>div{position:absolute;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Violations.Should().NotContain(v => v.RuleId == "forbidden.position.absolute",
            because: "LegacyPrintPolicy must allow position:absolute");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.position.absolute",
            because: "DefaultStrictPolicy must still block position:absolute");
    }

    [Fact]
    public async Task BorderCollapseCollapse_PassesLegacy_FailsDefaultStrict()
    {
        const string html = "<html><head><style>table{border-collapse:collapse;}</style></head><body><table><tr><td>x</td></tr></table></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Violations.Should().NotContain(v => v.RuleId == "forbidden.border-collapse.collapse",
            because: "LegacyPrintPolicy must allow border-collapse:collapse");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.border-collapse.collapse",
            because: "DefaultStrictPolicy must still block border-collapse:collapse");
    }

    // --- Block-list: both policies block these ---

    [Fact]
    public async Task DisplayFlex_FailsBothPolicies()
    {
        const string html = "<html><head><style>div{display:flex;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Violations.Should().Contain(v => v.RuleId == "forbidden.display.flex",
            because: "LegacyPrintPolicy must block display:flex");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.display.flex",
            because: "DefaultStrictPolicy must block display:flex");
    }

    [Fact]
    public async Task PositionFixed_FailsBothPolicies()
    {
        const string html = "<html><head><style>div{position:fixed;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Violations.Should().Contain(v => v.RuleId == "forbidden.position.fixed",
            because: "LegacyPrintPolicy must block position:fixed");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.position.fixed",
            because: "DefaultStrictPolicy must block position:fixed");
    }

    [Fact]
    public async Task ScriptElement_FailsBothPolicies()
    {
        const string html = "<html><body><script>alert(1)</script><p>x</p></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Violations.Should().Contain(v => v.RuleId == "forbidden.script-element",
            because: "LegacyPrintPolicy must block <script> elements");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.script-element",
            because: "DefaultStrictPolicy must block <script> elements");
    }

    [Fact]
    public async Task JavascriptHref_FailsBothPolicies()
    {
        const string html = "<html><body><a href=\"javascript:alert(1)\">x</a></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Violations.Should().Contain(v => v.RuleId == "forbidden.link.scheme",
            because: "LegacyPrintPolicy must block javascript: href");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.link.scheme",
            because: "DefaultStrictPolicy must block javascript: href");
    }

    // --- LegacyPrintPolicy identity check ---

    [Fact]
    public void LegacyPrintPolicy_Id_IsLegacyPrintV1()
    {
        var policy = new LegacyPrintPolicy();
        policy.Id.Should().Be("legacy-print-v1");
    }
}
