using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Abstractions.Telemetry;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Internal.Telemetry;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Pdf.Internal.Service;

/// <summary>
/// End-to-end HTML/CSS → PDF orchestrator. Drives parse → cascade → policy → layout → write,
/// enforces the render timeout via a linked <see cref="CancellationTokenSource"/> (PIPE-08),
/// and emits a <c>pdf.render</c> span plus operation/page-count metrics (TEL-02/03/04).
/// Singleton-safe: the scoped <see cref="ITenantContext"/> is resolved per-call via
/// <see cref="IServiceProvider"/> to avoid a captive-dependency scope violation (T-06-05).
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfInputLimitException and PdfPolicyException are public PDF-contract exception types; consumers catch them directly. Cannot change hierarchy.")]
internal sealed class MPdfService(
    IHtmlParser htmlParser,
    ICssCascadeEngine cascadeEngine,
    IPdfCssPolicy cssPolicy,
    IPdfWriter writer,
    IImageDecoder imageDecoder,
    IResourceResolver resourceResolver,
    IOptions<PdfConfigs> configs,
    IServiceProvider serviceProvider,
    IMLog<MPdfService> log,
    IFontResolver? fontResolver = null) : IMPdfService
{
    private readonly IHtmlParser _htmlParser = MGuard.NotNull(htmlParser);
    private readonly ICssCascadeEngine _cascadeEngine = MGuard.NotNull(cascadeEngine);
    private readonly IPdfCssPolicy _cssPolicy = MGuard.NotNull(cssPolicy);
    private readonly IPdfWriter _writer = MGuard.NotNull(writer);
    private readonly IImageDecoder _imageDecoder = MGuard.NotNull(imageDecoder);
    private readonly IResourceResolver _resourceResolver = MGuard.NotNull(resourceResolver);
    private readonly IServiceProvider _serviceProvider = MGuard.NotNull(serviceProvider);
    private readonly IMLog<MPdfService> _log = MGuard.NotNull(log);
    private readonly IFontResolver? _fontResolver = fontResolver;
    private readonly PdfConfigs _configs = MGuard.NotNull(configs).Value;

    public async Task<PdfRenderResult> RenderAsync(
        string html,
        Stream destination,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(html);
        MGuard.NotNull(destination);
        MGuard.NotNull(options);

        // T-06-04: size-gate untrusted HTML before parsing — early exit.
        int htmlBytes = Encoding.UTF8.GetByteCount(html);
        if (htmlBytes > _configs.Limits.MaxHtmlBytes)
        {
            throw new PdfInputLimitException(
                "HTML-MAX-BYTES", "MaxHtmlBytes", htmlBytes, _configs.Limits.MaxHtmlBytes);
        }

        // T-06-03 / PIPE-08: linked CTS + timeout. Threaded through every downstream await.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(_configs.Limits.MaxRenderDurationMs));

        using Activity? activity = PdfMetrics.Source.StartActivity("pdf.render", ActivityKind.Internal);
        string tenantId = _serviceProvider.GetService<ITenantContext>()?.TenantId ?? "unknown";
        activity?.SetTag(PdfTelemetryNames.TemplateIdTag, options.TemplateId ?? string.Empty);
        activity?.SetTag(PdfTelemetryNames.TenantIdTag, tenantId);

        var sw = Stopwatch.StartNew();
        try
        {
            IParsedDocument parsed = await _htmlParser.ParseAsync(html, cts.Token).ConfigureAwait(false);
            IStyledDocument styled = await _cascadeEngine.CascadeAsync(parsed, null, cts.Token).ConfigureAwait(false);

            if (styled is not IPdfDocumentContext documentContext)
            {
                throw new MInternalException(
                    "Styled document must implement IPdfDocumentContext for policy validation.");
            }

            PolicyValidationResult policy = await _cssPolicy
                .ValidateAsync(documentContext, cts.Token).ConfigureAwait(false);
            if (!policy.Accepted)
            {
                throw new PdfPolicyException(policy.Violations);
            }

            RunningContentSpec? running = await BuildRunningContentAsync(options, cts.Token).ConfigureAwait(false);

            var layout = new LayoutEngine();
            IPositionedPageList pages = await layout.LayoutAsync(
                styled, options, _configs.Limits, _fontResolver, _resourceResolver, _imageDecoder, cts.Token, running)
                .ConfigureAwait(false);

            long byteCount = await _writer.WriteAsync(pages, options, destination, cts.Token).ConfigureAwait(false);
            sw.Stop();

            int pageCount = (pages as PositionedPageList)?.PageCount ?? 0;

            PdfMetrics.OperationCounter.Add(
                1, new TagList { { PdfTelemetryNames.TenantIdTag, tenantId }, { "pdf.status", "ok" } });
            PdfMetrics.PageCountHistogram.Record(
                pageCount, new TagList { { PdfTelemetryNames.TenantIdTag, tenantId } });
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new PdfRenderResult(
                pageCount, byteCount, sw.Elapsed, string.Empty, _cssPolicy.Id, policy.Violations);
        }
        catch (OperationCanceledException)
        {
            // SC4 / Pitfall 4: must precede the general catch; propagate unmodified.
            activity?.SetStatus(ActivityStatusCode.Error, "timeout_or_cancelled");
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            PdfMetrics.OperationCounter.Add(
                1, new TagList { { PdfTelemetryNames.TenantIdTag, tenantId }, { "pdf.status", "error" } });
            _log.LogError(ex, "PDF render failed for template {TemplateId}", options.TemplateId ?? string.Empty);
            throw;
        }
    }

    // Phase 13: build the full-HTML running header/footer spec from options.Header/Footer.
    // Each non-empty column fragment is wrapped (base font + forced text-align), parsed, and
    // cascaded into a styled fragment document. LayoutEngine lays them out + stamps per page.
    private async Task<RunningContentSpec?> BuildRunningContentAsync(
        PdfRenderOptions options, CancellationToken ct)
    {
        PdfHeaderFooter? header = options.Header;
        PdfHeaderFooter? footer = options.Footer;
        if (header is null && footer is null) return null;

        async Task<IStyledDocument?> ColumnAsync(string? fragment, string align)
        {
            if (string.IsNullOrWhiteSpace(fragment)) return null;
            string wrapper =
                "<html><head><style>" +
                "html,body{margin:0;padding:0;}" +
                "html,body,p,div,span,td,th{font-family:\"Times New Roman\";font-size:11px;}" +
                ".hf{text-align:" + align + ";}" +
                "</style></head><body><div class=\"hf\">" + fragment + "</div></body></html>";

            IParsedDocument parsed = await _htmlParser.ParseAsync(wrapper, ct).ConfigureAwait(false);
            return await _cascadeEngine.CascadeAsync(parsed, null, ct).ConfigureAwait(false);
        }

        return new RunningContentSpec
        {
            HeaderLeft = header is null ? null : await ColumnAsync(header.LeftHtml, "left").ConfigureAwait(false),
            HeaderCenter = header is null ? null : await ColumnAsync(header.CenterHtml, "center").ConfigureAwait(false),
            HeaderRight = header is null ? null : await ColumnAsync(header.RightHtml, "right").ConfigureAwait(false),
            FooterLeft = footer is null ? null : await ColumnAsync(footer.LeftHtml, "left").ConfigureAwait(false),
            FooterCenter = footer is null ? null : await ColumnAsync(footer.CenterHtml, "center").ConfigureAwait(false),
            FooterRight = footer is null ? null : await ColumnAsync(footer.RightHtml, "right").ConfigureAwait(false),
            HeaderHeightPt = (float)((header?.HeightMm ?? 0d) * Units.MmToPt),
            FooterHeightPt = (float)((footer?.HeightMm ?? 0d) * Units.MmToPt),
            HeaderShowLine = header?.ShowLine ?? false,
            FooterShowLine = footer?.ShowLine ?? false,
            LineColor = "#888888",
        };
    }

    public async Task<(byte[] Bytes, PdfRenderResult Metadata)> RenderToBytesAsync(
        string html,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        PdfRenderResult meta = await RenderAsync(html, ms, options, cancellationToken).ConfigureAwait(false);
        return (ms.ToArray(), meta);
    }

    public async Task<PdfRenderResult> RenderMultiPageAsync(
        IReadOnlyList<string> htmlPages,
        Stream destination,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(htmlPages);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);

        if (htmlPages.Count == 0)
        {
            return new PdfRenderResult(
                0, 0, TimeSpan.Zero, string.Empty, _cssPolicy.Id, Array.Empty<PolicyViolation>());
        }

        // Layout-level merge: collect each fragment's PositionedPageList, concatenate into a
        // single combined list, and write one PDF. This avoids any third-party PDF parsing and
        // works with OwnedPdfWriter's pure-managed structure.
        var allPages = new PositionedPageList();
        var allFonts = new Dictionary<string, EmbeddedFontInfo>(StringComparer.Ordinal);
        var allImages = new Dictionary<string, DecodedImage>(StringComparer.Ordinal);
        var diagnostics = new List<PolicyViolation>();
        int globalPageIndex = 0;
        TimeSpan totalElapsed = TimeSpan.Zero;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(_configs.Limits.MaxRenderDurationMs * htmlPages.Count));

        var sw = Stopwatch.StartNew();

        foreach (string fragmentHtml in htmlPages)
        {
            int htmlBytes = Encoding.UTF8.GetByteCount(fragmentHtml);
            if (htmlBytes > _configs.Limits.MaxHtmlBytes)
                throw new PdfInputLimitException("HTML-MAX-BYTES", "MaxHtmlBytes", htmlBytes, _configs.Limits.MaxHtmlBytes);

            IParsedDocument parsed = await _htmlParser.ParseAsync(fragmentHtml, cts.Token).ConfigureAwait(false);
            IStyledDocument styled = await _cascadeEngine.CascadeAsync(parsed, null, cts.Token).ConfigureAwait(false);

            if (styled is not IPdfDocumentContext documentContext)
                throw new MInternalException("Styled document must implement IPdfDocumentContext for policy validation.");

            PolicyValidationResult policy = await _cssPolicy.ValidateAsync(documentContext, cts.Token).ConfigureAwait(false);
            if (!policy.Accepted)
                throw new PdfPolicyException(policy.Violations);

            if (policy.Violations.Count > 0)
                diagnostics.AddRange(policy.Violations);

            var layout = new LayoutEngine();
            IPositionedPageList fragmentPages = await layout.LayoutAsync(
                styled, options, _configs.Limits, _fontResolver, _resourceResolver, _imageDecoder, cts.Token)
                .ConfigureAwait(false);

            if (fragmentPages is PositionedPageList pageList)
            {
                foreach (PositionedPage page in pageList.Pages)
                {
                    var reindexed = new PositionedPage { PageIndex = globalPageIndex++ };
                    reindexed.Elements.AddRange(page.Elements.Select(e => new PositionedElement
                    {
                        Source = e.Source,
                        RenderedText = e.RenderedText,
                        Position = e.Position,
                        PageIndex = reindexed.PageIndex
                    }));
                    allPages.Pages.Add(reindexed);
                }

                foreach (EmbeddedFontInfo fi in pageList.EmbeddedFonts)
                {
                    if (!allFonts.ContainsKey(fi.Family))
                        allFonts[fi.Family] = fi;
                }

                foreach (KeyValuePair<string, DecodedImage> kv in pageList.Images)
                {
                    allImages.TryAdd(kv.Key, kv.Value);
                }
            }
        }

        allPages.EmbeddedFonts = allFonts.Values.ToList();
        allPages.Images = allImages;

        sw.Stop();
        totalElapsed = sw.Elapsed;

        using Activity? activity = PdfMetrics.Source.StartActivity("pdf.render.multi", ActivityKind.Internal);
        string tenantId = _serviceProvider.GetService<ITenantContext>()?.TenantId ?? "unknown";
        activity?.SetTag(PdfTelemetryNames.TemplateIdTag, options.TemplateId ?? string.Empty);
        activity?.SetTag(PdfTelemetryNames.TenantIdTag, tenantId);

        long byteCount = await _writer.WriteAsync(allPages, options, destination, cancellationToken).ConfigureAwait(false);

        PdfMetrics.OperationCounter.Add(
            1, new TagList { { PdfTelemetryNames.TenantIdTag, tenantId }, { "pdf.status", "ok" } });
        PdfMetrics.PageCountHistogram.Record(
            allPages.PageCount, new TagList { { PdfTelemetryNames.TenantIdTag, tenantId } });
        activity?.SetStatus(ActivityStatusCode.Ok);

        return new PdfRenderResult(
            allPages.PageCount, byteCount, totalElapsed, string.Empty, _cssPolicy.Id, diagnostics);
    }
}
