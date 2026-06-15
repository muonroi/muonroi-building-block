using Microsoft.AspNetCore.Mvc;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.DesignSystem;
using Muonroi.Pdf.Enterprise;
using Muonroi.Pdf.Enterprise.Quality;

namespace Quickstart.Pdf.Advanced.Api.Controllers;

/// <summary>
/// Demonstrates the design-system templates, the OSS PDF engine, and the
/// Enterprise quality toolkit together.
/// </summary>
[ApiController]
[Route("api/pdf")]
public sealed class PdfController(IMPdfService pdfService, IFeatureGate featureGate) : ControllerBase
{
    /// <summary>
    /// Returns the raw HTML of an embedded design-system template.
    /// GET /api/pdf/templates/invoice
    /// </summary>
    [HttpGet("templates/{name}")]
    public IActionResult GetTemplate(string name)
    {
        try
        {
            // DesignSystemTemplateProvider.GetTemplate serves invoice/receipt/report
            // HTML embedded in Muonroi.Pdf.DesignSystem.Default.
            string html = DesignSystemTemplateProvider.GetTemplate(name);
            return Content(html, "text/html");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Renders a design-system template to PDF after substituting {{Token}} values.
    /// POST /api/pdf/render/receipt  body: { "CompanyName": "Acme", "Total": "$42.00" }
    /// </summary>
    [HttpPost("render/{name}")]
    public async Task<IActionResult> Render(
        string name,
        [FromBody] Dictionary<string, string> tokens,
        CancellationToken ct)
    {
        string html;
        try
        {
            html = DesignSystemTemplateProvider.GetTemplate(name);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        // Simple {{Token}} substitution. The design system uses {{TokenName}} placeholders.
        foreach ((string key, string value) in tokens)
        {
            html = html.Replace("{{" + key + "}}", value ?? string.Empty);
        }

        // Render via the OSS engine. RenderToBytesAsync buffers the document and
        // returns the bytes plus render metadata (page count, timing, etc.).
        (byte[] bytes, PdfRenderResult metadata) = await pdfService.RenderToBytesAsync(
            html,
            new PdfRenderOptions(),
            ct);

        Response.Headers["X-Pdf-Pages"] = metadata.PageCount.ToString();
        return File(bytes, "application/pdf", $"{name}.pdf");
    }

    /// <summary>
    /// Demonstrates the Enterprise SSIM quality scorer — the visual-regression
    /// gate used to compare a reference render against a candidate render.
    /// GET /api/pdf/quality/self-check  (compares an 8x8 RGB buffer to itself = 1.0)
    /// </summary>
    [HttpGet("quality/self-check")]
    public IActionResult QualitySelfCheck()
    {
        const int width = 8;
        const int height = 8;
        byte[] reference = new byte[width * height * 3];
        Random.Shared.NextBytes(reference);

        // Identical buffers must score exactly 1.0 (perfect structural similarity).
        double identical = SsimScorer.Compare(reference, reference, width, height);

        // A divergent buffer scores lower.
        byte[] candidate = (byte[])reference.Clone();
        for (int i = 0; i < candidate.Length; i++)
        {
            candidate[i] = (byte)(255 - candidate[i]);
        }
        double inverted = SsimScorer.Compare(reference, candidate, width, height);

        return Ok(new
        {
            description = "Muonroi.Pdf.Enterprise SsimScorer — structural similarity quality gate.",
            identicalScore = identical,
            invertedScore = inverted,
            note = "Identical buffers score 1.0; the canary gate flags renders below a threshold."
        });
    }

    /// <summary>
    /// Demonstrates the Enterprise capability model.
    /// GET /api/pdf/capabilities
    /// </summary>
    [HttpGet("capabilities")]
    public IActionResult Capabilities()
    {
        // EnsureFeatureOrThrow is a no-op under AlwaysAllowFeatureGate (OSS/dev).
        featureGate.EnsureFeatureOrThrow(CapabilityKeys.PdfDesigner);

        return Ok(new
        {
            gate = featureGate.GetType().Name,
            capabilities = new
            {
                designer = new { key = CapabilityKeys.PdfDesigner, enabled = featureGate.IsEnabled(CapabilityKeys.PdfDesigner) },
                registry = new { key = CapabilityKeys.PdfRegistry, enabled = featureGate.IsEnabled(CapabilityKeys.PdfRegistry) },
                canary = new { key = CapabilityKeys.PdfCanary, enabled = featureGate.IsEnabled(CapabilityKeys.PdfCanary) }
            }
        });
    }
}
