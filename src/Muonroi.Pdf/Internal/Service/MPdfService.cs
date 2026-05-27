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
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Telemetry;
using Muonroi.Tenancy.Abstractions;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Muonroi.Pdf.Internal.Service;

/// <summary>
/// End-to-end HTML/CSS → PDF orchestrator. Drives parse → cascade → policy → layout → write,
/// enforces the render timeout via a linked <see cref="CancellationTokenSource"/> (PIPE-08),
/// and emits a <c>pdf.render</c> span plus operation/page-count metrics (TEL-02/03/04).
/// Singleton-safe: the scoped <see cref="ITenantContext"/> is resolved per-call via
/// <see cref="IServiceProvider"/> to avoid a captive-dependency scope violation (T-06-05).
/// </summary>
internal sealed class MPdfService : IMPdfService
{
    private readonly IHtmlParser _htmlParser;
    private readonly ICssCascadeEngine _cascadeEngine;
    private readonly IPdfCssPolicy _cssPolicy;
    private readonly IPdfWriter _writer;
    private readonly IImageDecoder _imageDecoder;
    private readonly IResourceResolver _resourceResolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMLog<MPdfService> _log;
    private readonly IFontResolver? _fontResolver;
    private readonly PdfConfigs _configs;

    public MPdfService(
        IHtmlParser htmlParser,
        ICssCascadeEngine cascadeEngine,
        IPdfCssPolicy cssPolicy,
        IPdfWriter writer,
        IImageDecoder imageDecoder,
        IResourceResolver resourceResolver,
        IOptions<PdfConfigs> configs,
        IServiceProvider serviceProvider,
        IMLog<MPdfService> log,
        IFontResolver? fontResolver = null)
    {
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _cascadeEngine = cascadeEngine ?? throw new ArgumentNullException(nameof(cascadeEngine));
        _cssPolicy = cssPolicy ?? throw new ArgumentNullException(nameof(cssPolicy));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _imageDecoder = imageDecoder ?? throw new ArgumentNullException(nameof(imageDecoder));
        _resourceResolver = resourceResolver ?? throw new ArgumentNullException(nameof(resourceResolver));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _fontResolver = fontResolver;
        _configs = (configs ?? throw new ArgumentNullException(nameof(configs))).Value;
    }

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

        var fragments = new List<MemoryStream>(htmlPages.Count);
        var diagnostics = new List<PolicyViolation>();
        int totalPages = 0;
        TimeSpan totalElapsed = TimeSpan.Zero;
        try
        {
            foreach (string fragmentHtml in htmlPages)
            {
                var fragmentStream = new MemoryStream();
                PdfRenderResult meta = await RenderAsync(fragmentHtml, fragmentStream, options, cancellationToken)
                    .ConfigureAwait(false);
                fragmentStream.Position = 0;
                fragments.Add(fragmentStream);
                totalPages += meta.PageCount;
                totalElapsed += meta.Elapsed;
                if (meta.Diagnostics.Count > 0)
                {
                    diagnostics.AddRange(meta.Diagnostics);
                }
            }

            using var output = new PdfDocument();
            foreach (MemoryStream fragment in fragments)
            {
                fragment.Position = 0;
                using PdfDocument input = PdfReader.Open(fragment, PdfDocumentOpenMode.Import);
                foreach (PdfPage page in input.Pages)
                {
                    output.AddPage(page);
                }
            }

            long startPos = destination.CanSeek ? destination.Position : 0;
            output.Save(destination, closeStream: false);
            long byteCount = destination.CanSeek ? destination.Position - startPos : 0;

            return new PdfRenderResult(
                totalPages, byteCount, totalElapsed, string.Empty, _cssPolicy.Id, diagnostics);
        }
        finally
        {
            foreach (MemoryStream fragment in fragments)
            {
                fragment.Dispose();
            }
        }
    }
}
