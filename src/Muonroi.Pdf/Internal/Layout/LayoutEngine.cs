using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Image;
using Muonroi.Pdf.Internal.Layout.Geometry;
using SixLabors.Fonts;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class LayoutEngine
{
    private readonly BoxTreeBuilder _boxTreeBuilder;
    private readonly BlockLayoutEngine _blockEngine;
    private readonly PaginationEngine _paginationEngine;
    private readonly ITextMetrics _textMetrics;

    public LayoutEngine() : this(EstimatedTextMetrics.Instance) { }

    public LayoutEngine(ITextMetrics textMetrics)
    {
        _textMetrics = textMetrics;
        _boxTreeBuilder = new BoxTreeBuilder();
        _blockEngine = new BlockLayoutEngine();
        var tableEngine = new TableLayoutEngine(_blockEngine, _blockEngine.InlineEngine);
        _blockEngine.TableEngine = tableEngine;
        _paginationEngine = new PaginationEngine();
    }

    public IPositionedPageList Layout(
        IStyledDocument doc,
        PdfRenderOptions options,
        PdfConfigs.PdfLimits limits,
        CancellationToken ct)
    {
        var pass1 = RunLayout(doc, options, totalPages: 0);

        if (pass1.PageCount > PdfConfigs.PdfLimits.Defaults.MaxPages)
            throw new PdfInputLimitException(
                "PAGE-MAX-PAGES",
                "MaxPages",
                pass1.PageCount,
                PdfConfigs.PdfLimits.Defaults.MaxPages);

        ct.ThrowIfCancellationRequested();

        return RunLayout(doc, options, totalPages: pass1.PageCount);
    }

    public async Task<IPositionedPageList> LayoutAsync(
        IStyledDocument doc,
        PdfRenderOptions options,
        PdfConfigs.PdfLimits limits,
        IFontResolver? fontResolver,
        IResourceResolver? imageResolver,
        IImageDecoder imageDecoder,
        CancellationToken ct)
    {
        SixLaborsTextMetrics? realMetrics = null;
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> fontBytesMap = new Dictionary<string, ReadOnlyMemory<byte>>();
        FontCollection? fontCollection = null;

        if (fontResolver != null)
        {
            var fontPipeline = new FontPipeline();
            (realMetrics, fontBytesMap, fontCollection) = await fontPipeline.ResolveAsync(doc, fontResolver, limits, ct).ConfigureAwait(false);
        }

        IReadOnlyDictionary<string, DecodedImage> resolvedImages;
        if (imageResolver != null)
        {
            var imagePipeline = new ImagePipeline();
            resolvedImages = await imagePipeline.ResolveAsync(doc, imageResolver, imageDecoder, limits, ct).ConfigureAwait(false);
        }
        else
        {
            resolvedImages = new Dictionary<string, DecodedImage>();
        }

        LayoutEngine engineToUse = fontResolver != null && realMetrics != null
            ? new LayoutEngine(realMetrics)
            : this;

        var pass1 = engineToUse.RunLayout(doc, options, totalPages: 0, resolvedImages);

        if (pass1.PageCount > PdfConfigs.PdfLimits.Defaults.MaxPages)
            throw new PdfInputLimitException(
                "PAGE-MAX-PAGES",
                "MaxPages",
                pass1.PageCount,
                PdfConfigs.PdfLimits.Defaults.MaxPages);

        ct.ThrowIfCancellationRequested();

        var pass2 = engineToUse.RunLayout(doc, options, totalPages: pass1.PageCount, resolvedImages);

        var embeddedFonts = new List<EmbeddedFontInfo>();
        if (fontResolver != null && fontBytesMap.Count > 0 && fontCollection != null)
        {
            var collector = new GlyphCollector();
            IReadOnlyDictionary<string, IReadOnlySet<int>> usedCodepoints = collector.Collect(pass2, fontCollection);

            foreach (KeyValuePair<string, ReadOnlyMemory<byte>> kvp in fontBytesMap)
            {
                string family = kvp.Key;
                IReadOnlySet<int> codepoints = usedCodepoints.TryGetValue(family, out IReadOnlySet<int>? cp) ? cp : new HashSet<int>();
                var subsetter = new TrueTypeFontSubsetter();
                ReadOnlyMemory<byte> subsetBytes = subsetter.Subset(kvp.Value, codepoints);

                FontFaceDeclaration? decl = doc.FontFaces.FirstOrDefault(f => f.Family == family);
                if (decl == null)
                    continue;

                embeddedFonts.Add(new EmbeddedFontInfo(decl.Family, decl.Weight, decl.Style, subsetBytes, codepoints));
            }
        }

        pass2.EmbeddedFonts = embeddedFonts;
        pass2.Images = resolvedImages;

        return pass2;
    }

    private PositionedPageList RunLayout(IStyledDocument doc, PdfRenderOptions options, int totalPages, IReadOnlyDictionary<string, DecodedImage>? resolvedImages = null)
    {
        var (pageWidthPt, pageHeightPt) = GetPageDimensions(options);
        var margins = ResolveMargins(options, doc.PageRule);

        float topMarginPt = (float)(margins.TopMm * Units.MmToPt);
        float bottomMarginPt = (float)(margins.BottomMm * Units.MmToPt);
        float leftMarginPt = (float)(margins.LeftMm * Units.MmToPt);
        float rightMarginPt = (float)(margins.RightMm * Units.MmToPt);
        float pageBodyHeight = pageHeightPt - topMarginPt - bottomMarginPt;
        float availableWidth = pageWidthPt - leftMarginPt - rightMarginPt;

        var rootBox = _boxTreeBuilder.Build(doc.Root, resolvedImages);

        var context = new LayoutContext
        {
            PageWidth = pageWidthPt,
            PageHeight = pageHeightPt,
            AvailableWidth = availableWidth,
            CurrentY = topMarginPt,
            CurrentPageIndex = 0,
            TotalPages = totalPages,
            TextMetrics = _textMetrics,
            PageMargins = margins
        };

        var elements = new List<PositionedElement>();
        _blockEngine.Layout(rootBox, context, elements, 0, isRoot: true);

        return _paginationEngine.Paginate(
            elements,
            pageBodyHeight,
            topMarginPt,
            bottomMarginPt,
            pageWidthPt,
            totalPages,
            doc.PageRule,
            options);
    }

    private static (float Width, float Height) GetPageDimensions(PdfRenderOptions options)
    {
        var (w, h) = PdfPageSizeDimensions.Get(options.PageSize);
        return options.Orientation == PdfOrientation.Landscape ? (h, w) : (w, h);
    }

    // Decision 3: options.Margins wins if explicitly set (differs from Default10mm);
    // otherwise @page margins apply; finally fall back to Default10mm.
    private static PdfMargins ResolveMargins(PdfRenderOptions options, IPageRule? pageRule)
    {
        if (options.Margins != PdfMargins.Default10mm)
            return options.Margins;
        if (pageRule != null)
            return pageRule.Margins;
        return PdfMargins.Default10mm;
    }
}
