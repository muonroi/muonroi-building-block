using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.Logging.Abstractions;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Abstractions.Telemetry;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
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
    private readonly IHtmlParser _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
    private readonly ICssCascadeEngine _cascadeEngine = cascadeEngine ?? throw new ArgumentNullException(nameof(cascadeEngine));
    private readonly IPdfCssPolicy _cssPolicy = cssPolicy ?? throw new ArgumentNullException(nameof(cssPolicy));
    private readonly IPdfWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly IImageDecoder _imageDecoder = imageDecoder ?? throw new ArgumentNullException(nameof(imageDecoder));
    private readonly IResourceResolver _resourceResolver = resourceResolver ?? throw new ArgumentNullException(nameof(resourceResolver));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IMLog<MPdfService> _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IFontResolver? _fontResolver = fontResolver;
    private readonly PdfConfigs _configs = (configs ?? throw new ArgumentNullException(nameof(configs))).Value;

    public async Task<PdfRenderResult> RenderAsync(
        string html,
        Stream destination,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);

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
                throw new InvalidOperationException(
                    "Styled document must implement IPdfDocumentContext for policy validation.");
            }

            PolicyValidationResult policy = await _cssPolicy
                .ValidateAsync(documentContext, cts.Token).ConfigureAwait(false);
            if (!policy.Accepted)
            {
                throw new PdfPolicyException(policy.Violations);
            }

            var layout = new LayoutEngine();
            IPositionedPageList pages = await layout.LayoutAsync(
                styled, options, _configs.Limits, _fontResolver, _resourceResolver, _imageDecoder, cts.Token)
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
                throw new InvalidOperationException("Styled document must implement IPdfDocumentContext for policy validation.");

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
