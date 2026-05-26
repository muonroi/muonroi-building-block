using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Layout.Geometry;

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

        if (pass1.PageCount > PdfConfigs.PdfLimits.MaxPages)
            throw new PdfInputLimitException(
                "PAGE-MAX-PAGES",
                "MaxPages",
                pass1.PageCount,
                PdfConfigs.PdfLimits.MaxPages);

        ct.ThrowIfCancellationRequested();

        return RunLayout(doc, options, totalPages: pass1.PageCount);
    }

    private PositionedPageList RunLayout(IStyledDocument doc, PdfRenderOptions options, int totalPages)
    {
        var (pageWidthPt, pageHeightPt) = GetPageDimensions(options);
        var margins = ResolveMargins(options, doc.PageRule);

        float topMarginPt = (float)(margins.TopMm * Units.MmToPt);
        float bottomMarginPt = (float)(margins.BottomMm * Units.MmToPt);
        float leftMarginPt = (float)(margins.LeftMm * Units.MmToPt);
        float rightMarginPt = (float)(margins.RightMm * Units.MmToPt);
        float pageBodyHeight = pageHeightPt - topMarginPt - bottomMarginPt;
        float availableWidth = pageWidthPt - leftMarginPt - rightMarginPt;

        var rootBox = _boxTreeBuilder.Build(doc.Root);

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
