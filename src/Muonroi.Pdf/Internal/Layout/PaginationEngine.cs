using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

// Distributes PositionedElements across pages, applies counter substitution, and stamps the
// full-HTML running header/footer (Phase 13) onto every page.
//
// PITFALL 4 (RESEARCH.md): page-break-inside:avoid with element taller than a full page
// must still be placed (break anyway) to prevent an infinite loop.
internal sealed class PaginationEngine
{
    // Separator-rule thickness in points when PdfHeaderFooter.ShowLine is set.
    private const float LineThicknessPt = 0.7f;

    // Paginate body elements into pages, then stamp the rendered running header/footer.
    //
    // elements         – body positioned boxes (Y in continuous body-space)
    // pageBodyHeight   – usable body height per page (pageHeight − effectiveTop − effectiveBottom)
    // pageTopMarginPt  – EFFECTIVE top margin (max of CSS margin and header band) where body starts
    // pageBottomMarginPt – EFFECTIVE bottom margin (max of CSS margin and footer band)
    // pageWidth/pageHeight – full page dimensions in points
    // totalPages       – for counter(pages) substitution (0 in pass 1, actual count in pass 2)
    // running          – laid-out header/footer columns to stamp (null = none)
    public PositionedPageList Paginate(
        List<PositionedElement> elements,
        float pageBodyHeight,
        float pageTopMarginPt,
        float pageBottomMarginPt,
        float pageWidth,
        float pageHeight,
        int totalPages,
        RenderedRunningContent? running)
    {
        var result = new PositionedPageList();

        if (elements.Count == 0)
        {
            result.Pages.Add(new PositionedPage { PageIndex = 0 });
        }
        else
        {
            PaginateBody(elements, pageBodyHeight, pageTopMarginPt, pageWidth, result);
        }

        CollectLinkAnnotations(result);
        SubstituteBodyCounters(result, totalPages);
        StampRunningContent(result, running, pageWidth, pageHeight, totalPages);

        return result;
    }

    private static void PaginateBody(
        List<PositionedElement> elements,
        float pageBodyHeight,
        float pageTopMarginPt,
        float pageWidth,
        PositionedPageList result)
    {
        _ = pageWidth;

        // Sort by ascending Y so we scan document order.
        var sorted = elements.OrderBy(e => e.Position.Y).ThenBy(e => e.Position.X).ToList();

        int pageIndex = 0;
        float pageBodyStart = 0f;  // Y in continuous space where current page's body starts

        var currentPage = new PositionedPage { PageIndex = pageIndex };
        result.Pages.Add(currentPage);

        for (int i = 0; i < sorted.Count; i++)
        {
            var el = sorted[i];
            var src = el.Source;

            bool forceBreak = string.Equals(src.PageBreakBefore, "always", StringComparison.OrdinalIgnoreCase);

            float elBottom = el.Position.Y + el.Position.Height;
            bool naturalBreak = elBottom > pageBodyStart + pageBodyHeight;

            bool avoidBreak = string.Equals(src.PageBreakInside, "avoid", StringComparison.OrdinalIgnoreCase);
            bool fitsOnNextPage = el.Position.Height <= pageBodyHeight; // PITFALL 4: oversized element must still land

            if (forceBreak || (naturalBreak && (!avoidBreak || !fitsOnNextPage)))
            {
                pageIndex++;
                pageBodyStart = el.Position.Y;
                currentPage = new PositionedPage { PageIndex = pageIndex };
                result.Pages.Add(currentPage);
            }
            else if (naturalBreak && avoidBreak && fitsOnNextPage)
            {
                // page-break-inside:avoid: element doesn't fit on current page — start new page.
                pageIndex++;
                pageBodyStart = el.Position.Y;
                currentPage = new PositionedPage { PageIndex = pageIndex };
                result.Pages.Add(currentPage);
            }

            // Translate from continuous Y to page-local Y (body starts at the effective top margin).
            float localY = el.Position.Y - pageBodyStart + pageTopMarginPt;

            currentPage.Elements.Add(new PositionedElement
            {
                Position = new Rect(el.Position.X, localY, el.Position.Width, el.Position.Height),
                Source = el.Source,
                RenderedText = el.RenderedText,
                PageIndex = pageIndex
            });
        }
    }

    // Collect link annotations: scan positioned elements for InlineBox.LinkHref.
    private static void CollectLinkAnnotations(PositionedPageList result)
    {
        for (int p = 0; p < result.Pages.Count; p++)
        {
            foreach (var el in result.Pages[p].Elements)
            {
                if (el.Source is InlineBox linkBox && linkBox.LinkHref != null)
                {
                    result.Pages[p].LinkAnnotations.Add(new LinkAnnotation(
                        linkBox.LinkHref,
                        el.Position.X,
                        el.Position.Y,
                        el.Position.Width,
                        el.Position.Height,
                        p));
                }
            }
        }
    }

    // Counter substitution for BODY inline text (header/footer counters are substituted at stamp time).
    private static void SubstituteBodyCounters(PositionedPageList result, int totalPages)
    {
        for (int p = 0; p < result.Pages.Count; p++)
        {
            int oneBased = p + 1;
            foreach (var el in result.Pages[p].Elements)
            {
                if (el.Source is not InlineBox inlineBox) continue;
                if (string.IsNullOrEmpty(inlineBox.Text)) continue;
                if (!inlineBox.Text.Contains("counter(", StringComparison.Ordinal)) continue;

                inlineBox.Text = SubstituteCounters(inlineBox.Text, oneBased, totalPages) ?? inlineBox.Text;
                if (el.RenderedText is not null)
                    el.RenderedText = SubstituteCounters(el.RenderedText, oneBased, totalPages);
            }
        }
    }

    // Stamp the rendered running header/footer onto every page, cloning elements (footer translated
    // to the bottom band) and substituting page counters per page. Adds separator rules when requested.
    private static void StampRunningContent(
        PositionedPageList result,
        RenderedRunningContent? running,
        float pageWidth,
        float pageHeight,
        int totalPages)
    {
        if (running is null || (!running.HasHeader && !running.HasFooter)) return;

        float lineX = running.ContentLeftPt;
        float lineW = running.ContentWidthPt > 0f ? running.ContentWidthPt : pageWidth;

        for (int p = 0; p < result.Pages.Count; p++)
        {
            var page = result.Pages[p];
            int oneBased = p + 1;

            // Header band at top: Y stays band-local (band starts at page top, Y=0).
            foreach (var el in running.HeaderElements)
                page.Elements.Add(CloneStamped(el, el.Position.Y, oneBased, totalPages, p));

            if (running.HeaderShowLine && running.HeaderBandPt > 0f)
                page.Elements.Add(SeparatorRect(lineX, running.HeaderBandPt - LineThicknessPt, lineW, running.LineColor, p));

            // Footer band at bottom: translate band-local Y into [pageHeight − footerBandPt, pageHeight].
            float footerTop = pageHeight - running.FooterBandPt;
            foreach (var el in running.FooterElements)
                page.Elements.Add(CloneStamped(el, footerTop + el.Position.Y, oneBased, totalPages, p));

            if (running.FooterShowLine && running.FooterBandPt > 0f)
                page.Elements.Add(SeparatorRect(lineX, footerTop, lineW, running.LineColor, p));
        }
    }

    private static PositionedElement CloneStamped(
        PositionedElement el, float newY, int oneBased, int totalPages, int pageIndex)
    {
        BoxNode source = el.Source;

        // GlyphCollector reads InlineBox.Text (the source text), NOT RenderedText. The stamped
        // source still holds the literal "counter(page)/counter(pages)" — with no digits — so the
        // page-number glyphs would never enter the font subset and would render blank. Clone the
        // InlineBox with the per-page substituted Text so the digits are collected for THIS family.
        if (source is InlineBox ib && ib.Text is { } t && t.Contains("counter(", StringComparison.Ordinal))
        {
            source = new InlineBox
            {
                Text = SubstituteCounters(t, oneBased, totalPages),
                FontFamily = ib.FontFamily,
                FontSize = ib.FontSize,
                Italic = ib.Italic,
                Color = ib.Color,
                VerticalAlign = ib.VerticalAlign,
                LineHeightFactor = ib.LineHeightFactor,
                TextDecoration = ib.TextDecoration,
                LinkHref = ib.LinkHref,
                Bold = ib.Bold,
                TextTransform = ib.TextTransform,
                WhiteSpace = ib.WhiteSpace,
                WordBreak = ib.WordBreak,
                Display = ib.Display,
            };
        }

        return new PositionedElement
        {
            // The (possibly cloned) source is never shared-mutated across pages; the per-page
            // RenderedText carries the substituted page numbers, which the writer prefers.
            Source = source,
            RenderedText = SubstituteCounters(el.RenderedText, oneBased, totalPages),
            Position = new Rect(el.Position.X, newY, el.Position.Width, el.Position.Height),
            PageIndex = pageIndex
        };
    }

    // A thin filled rectangle drawn via an empty InlineBox carrying BackgroundColor — the writer
    // fills BackgroundColor for any source box regardless of type.
    private static PositionedElement SeparatorRect(
        float x, float y, float width, string? color, int pageIndex) =>
        new()
        {
            Source = new InlineBox { Text = null, BackgroundColor = color ?? "#888888" },
            Position = new Rect(x, y, width, LineThicknessPt),
            PageIndex = pageIndex
        };

    private static string? SubstituteCounters(string? s, int oneBased, int totalPages)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains("counter(", StringComparison.Ordinal))
            return s;
        return s
            .Replace("counter(pages)", totalPages.ToString(), StringComparison.Ordinal)
            .Replace("counter(page)", oneBased.ToString(), StringComparison.Ordinal);
    }
}
