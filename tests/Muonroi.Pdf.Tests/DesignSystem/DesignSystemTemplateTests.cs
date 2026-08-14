namespace Muonroi.Pdf.Tests.DesignSystem;

/// <summary>
/// DS-01: each design system template renders a non-empty PDF stream.
/// DS-02: each design system template has zero DefaultStrictPolicy violations.
/// </summary>
[Trait("Category", "DesignSystem")]
public sealed class DesignSystemTemplateTests
{
    /// <summary>
    /// Replaces all {{TokenName}} placeholders with a benign literal value so the HTML
    /// parses cleanly without real model data.
    /// </summary>
    private static string SubstituteTokens(string html) =>
        Regex.Replace(html, @"\{\{[^}]+\}\}", "placeholder");

    // ── DS-01 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DS01_Invoice_RendersNonEmptyPdf()
    {
        string html = SubstituteTokens(DesignSystemTemplateProvider.GetTemplate("invoice"));
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        using var ms = new MemoryStream();
        PdfRenderResult result = await svc.RenderAsync(html, ms, new PdfRenderOptions(), CancellationToken.None);

        ms.Length.Should().BeGreaterThan(0, "invoice template should produce a non-empty PDF");
        result.ByteCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DS01_Receipt_RendersNonEmptyPdf()
    {
        string html = SubstituteTokens(DesignSystemTemplateProvider.GetTemplate("receipt"));
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        using var ms = new MemoryStream();
        PdfRenderResult result = await svc.RenderAsync(html, ms, new PdfRenderOptions(), CancellationToken.None);

        ms.Length.Should().BeGreaterThan(0, "receipt template should produce a non-empty PDF");
        result.ByteCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DS01_Report_RendersNonEmptyPdf()
    {
        string html = SubstituteTokens(DesignSystemTemplateProvider.GetTemplate("report"));
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        using var ms = new MemoryStream();
        PdfRenderResult result = await svc.RenderAsync(html, ms, new PdfRenderOptions(), CancellationToken.None);

        ms.Length.Should().BeGreaterThan(0, "report template should produce a non-empty PDF");
        result.ByteCount.Should().BeGreaterThan(0);
    }

    // ── DS-02 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DS02_Invoice_PassesDefaultStrictPolicy()
    {
        await AssertZeroViolationsAsync("invoice");
    }

    [Fact]
    public async Task DS02_Receipt_PassesDefaultStrictPolicy()
    {
        await AssertZeroViolationsAsync("receipt");
    }

    [Fact]
    public async Task DS02_Report_PassesDefaultStrictPolicy()
    {
        await AssertZeroViolationsAsync("report");
    }

    private static async Task AssertZeroViolationsAsync(string templateName)
    {
        string html = SubstituteTokens(DesignSystemTemplateProvider.GetTemplate(templateName));

        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html, CancellationToken.None);

        var cascade = new AngleSharpCascadeEngine();
        IStyledDocument styled = await cascade.CascadeAsync(parsed, null);

        var policy = new DefaultStrictPolicy();
        PolicyValidationResult result = await policy.ValidateAsync((IPdfDocumentContext)styled, CancellationToken.None);

        string violationList = result.Violations.Count == 0
            ? string.Empty
            : string.Join(", ", result.Violations.Select(v => $"{v.RuleId}:{v.CssSelector}={v.RejectedValue}"));

        result.Violations.Should().BeEmpty(
            $"template '{templateName}' has {result.Violations.Count} DefaultStrict violation(s): [{violationList}]");
    }
}
