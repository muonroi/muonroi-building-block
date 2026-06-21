using Microsoft.Extensions.Options;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;
using Muonroi.Pdf.Governance.Policies;

namespace Muonroi.Pdf.Tests.Policy;

/// <summary>
/// Validates the <c>PdfPolicySettings.AllowModernLayout</c> opt-in gate on
/// <see cref="LegacyPrintPolicy"/> (Phase 18, FLEX-02..04):
/// <list type="bullet">
///   <item>flag ON  → flex display + flex sub-properties are ACCEPTED (no violation)</item>
///   <item>flag ON  → CSS Grid stays blocked (only flex is unlocked)</item>
///   <item>flag OFF → flex behaviour is byte-for-byte unchanged (strict and soft-degrade)</item>
/// </list>
/// </summary>
public sealed class LegacyPrintPolicyAllowModernLayoutTests
{
    private static async Task<IPdfDocumentContext> ParseAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var cascade = new AngleSharpCascadeEngine();
        IStyledDocument doc = await cascade.CascadeAsync(parsed, null).ConfigureAwait(false);
        return (IPdfDocumentContext)doc;
    }

    private static LegacyPrintPolicy PolicyWith(bool allowModernLayout, bool softDegrade = false) =>
        new(Options.Create(new PdfConfigs
        {
            Policy = new PdfPolicySettings
            {
                AllowModernLayout = allowModernLayout,
                SoftDegradeUnknownDisplay = softDegrade,
            },
        }));

    // --- FLEX-02: flag ON accepts flex display + flex sub-properties ---

    [Fact]
    public async Task FlexWithSubProps_FlagOn_Accepted_NoFlexViolation()
    {
        const string html =
            "<html><head><style>div{display:flex;flex-direction:row;gap:10px;}</style></head>" +
            "<body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        LegacyPrintPolicy policy = PolicyWith(allowModernLayout: true);
        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Accepted.Should().BeTrue(because: "AllowModernLayout=true accepts flex");
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.display.flex");
        result.Violations.Should().NotContain(v => v.RuleId == "soft-degrade.display.flex");
        result.Violations.Should().NotContain(v => v.RuleId == "soft-degrade.flex-subproperty");
    }

    // --- FLEX-04: grid stays blocked even when the flag is ON ---

    [Fact]
    public async Task Grid_FlagOn_StrictBase_StillForbidden()
    {
        const string html = "<html><head><style>div{display:grid;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        LegacyPrintPolicy policy = PolicyWith(allowModernLayout: true, softDegrade: false);
        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Accepted.Should().BeFalse(because: "grid is not unlocked by AllowModernLayout");
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.display.grid");
    }

    [Fact]
    public async Task Grid_FlagOn_SoftDegrade_StillSoftDegradeWarning()
    {
        const string html = "<html><head><style>div{display:grid;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        LegacyPrintPolicy policy = PolicyWith(allowModernLayout: true, softDegrade: true);
        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Violations.Should().Contain(v =>
            v.RuleId == "soft-degrade.display.grid" && v.Severity == PolicySeverity.Warning);
    }

    // --- FLEX-03: flag OFF (default) is byte-for-byte unchanged ---

    [Fact]
    public async Task Flex_FlagOff_StrictDefault_StillForbidden_BothPolicies()
    {
        const string html = "<html><head><style>div{display:flex;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        var legacy = new LegacyPrintPolicy();            // default: flag off, strict
        var strict = new DefaultStrictPolicy();

        PolicyValidationResult legacyResult = await legacy.ValidateAsync(context);
        PolicyValidationResult strictResult = await strict.ValidateAsync(context);

        legacyResult.Accepted.Should().BeFalse();
        legacyResult.Violations.Should().Contain(v => v.RuleId == "forbidden.display.flex");
        strictResult.Violations.Should().Contain(v => v.RuleId == "forbidden.display.flex",
            because: "DefaultStrictPolicy ignores AllowModernLayout and always blocks flex");
    }

    [Fact]
    public async Task FlexWithSubProp_FlagOff_SoftDegrade_StillWarnsAndDegrades()
    {
        const string html =
            "<html><head><style>div{display:flex;flex-grow:1;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        LegacyPrintPolicy policy = PolicyWith(allowModernLayout: false, softDegrade: true);
        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Accepted.Should().BeTrue(because: "soft-degrade emits warnings only, block degrade preserved");
        result.Violations.Should().Contain(v =>
            v.RuleId == "soft-degrade.display.flex" && v.Severity == PolicySeverity.Warning);
        result.Violations.Should().Contain(v =>
            v.RuleId == "soft-degrade.flex-subproperty" && v.Severity == PolicySeverity.Warning);
    }
}
