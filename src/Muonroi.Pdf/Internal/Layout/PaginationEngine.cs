using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

// Distributes PositionedElements across pages, applies counter substitution, and
// appends per-page header/footer elements.
//
// PITFALL 4 (RESEARCH.md): page-break-inside:avoid with element taller than a full page
// must still be placed (break anyway) to prevent an infinite loop.
internal sealed class PaginationEngine
{
    // Paginate elements from continuous layout space into a PositionedPageList.
    //
    // elements  – all positioned boxes from BlockLayoutEngine (Y in continuous body-space)
    // pageBodyHeight – usable body height per page (pageHeight − topMargin − bottomMargin)
    // pageTopMarginPt – top margin in points (header sits above this; body starts at this Y on each physical page)
    // pageBottomMarginPt – bottom margin in points (footer sits below body)
    // pageWidth  – full page width in points
    // totalPages – for counter(pages) substitution (0 in pass 1, actual count in pass 2)
    // pageRule   – @page header/footer HTML; overridden by options if options supplies its own
    // options    – API-level rendering options
    public PositionedPageList Paginate(
        List<PositionedElement> elements,
        float pageBodyHeight,
        float pageTopMarginPt,
        float pageBottomMarginPt,
        float pageWidth,
        int totalPages,
        IPageRule? pageRule,
        PdfRenderOptions options)
    {
        // Resolve header/footer HTML: API options take precedence over @page rule.
        string? headerHtml = CombineHeaderFooter(options.Header) ?? pageRule?.TopMarginBoxHtml;
        string? footerHtml = CombineHeaderFooter(options.Footer) ?? pageRule?.BottomMarginBoxHtml;

        var result = new PositionedPageList();
        if (elements.Count == 0)
        {
            AddPage(result, 0, headerHtml, footerHtml, pageTopMarginPt, pageBottomMarginPt,
                pageWidth, pageBodyHeight, totalPages);
            return result;
        }

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

            // Translate from continuous Y to page-local Y.
            float localY = el.Position.Y - pageBodyStart + pageTopMarginPt;

            currentPage.Elements.Add(new PositionedElement
            {
                Position = new Rect(el.Position.X, localY, el.Position.Width, el.Position.Height),
                Source = el.Source,
                PageIndex = pageIndex
            });
        }

        // Collect link annotations: scan positioned elements for InlineBox.LinkHref
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

        // Counter substitution: replace counter(page) and counter(pages) in InlineBox text.
        for (int p = 0; p < result.Pages.Count; p++)
        {
            int oneBased = p + 1;
            foreach (var el in result.Pages[p].Elements)
            {
                if (el.Source is not InlineBox inlineBox) continue;
                if (string.IsNullOrEmpty(inlineBox.Text)) continue;
                if (!inlineBox.Text.Contains("counter(", StringComparison.Ordinal)) continue;

                inlineBox.Text = inlineBox.Text
                    .Replace("counter(pages)", totalPages.ToString(), StringComparison.Ordinal)
                    .Replace("counter(page)", oneBased.ToString(), StringComparison.Ordinal);
            }
        }

        // Append header/footer to every page.
        for (int p = 0; p < result.Pages.Count; p++)
            ApplyHeaderFooter(result.Pages[p], p, headerHtml, footerHtml,
                pageTopMarginPt, pageBottomMarginPt, pageWidth, pageBodyHeight, totalPages);

        return result;
    }

    private static void AddPage(PositionedPageList list, int pageIndex,
        string? headerHtml, string? footerHtml,
        float topMarginPt, float bottomMarginPt, float pageWidth, float bodyHeight, int totalPages)
    {
        var page = new PositionedPage { PageIndex = pageIndex };
        list.Pages.Add(page);
        ApplyHeaderFooter(page, pageIndex, headerHtml, footerHtml,
            topMarginPt, bottomMarginPt, pageWidth, bodyHeight, totalPages);
    }

    private static void ApplyHeaderFooter(PositionedPage page, int pageIndex,
        string? headerHtml, string? footerHtml,
        float topMarginPt, float bottomMarginPt, float pageWidth, float bodyHeight, int totalPages)
    {
        int oneBased = pageIndex + 1;

        if (!string.IsNullOrEmpty(headerHtml))
        {
            string text = StripTags(headerHtml)
                .Replace("counter(pages)", totalPages.ToString(), StringComparison.Ordinal)
                .Replace("counter(page)", oneBased.ToString(), StringComparison.Ordinal);

            var headerBox = new InlineBox { Text = text };
            page.Elements.Add(new PositionedElement
            {
                Position = new Rect(0f, 0f, pageWidth, topMarginPt),
                Source = headerBox,
                PageIndex = pageIndex
            });
        }

        if (!string.IsNullOrEmpty(footerHtml))
        {
            string text = StripTags(footerHtml)
                .Replace("counter(pages)", totalPages.ToString(), StringComparison.Ordinal)
                .Replace("counter(page)", oneBased.ToString(), StringComparison.Ordinal);

            float footerY = topMarginPt + bodyHeight;
            var footerBox = new InlineBox { Text = text };
            page.Elements.Add(new PositionedElement
            {
                Position = new Rect(0f, footerY, pageWidth, bottomMarginPt),
                Source = footerBox,
                PageIndex = pageIndex
            });
        }
    }

    private static string CombineHeaderFooter(PdfHeaderFooter? hf)
    {
        if (hf is null) return string.Empty;
        var parts = new[] { hf.LeftHtml, hf.CenterHtml, hf.RightHtml }
            .Where(s => !string.IsNullOrEmpty(s));
        return string.Join(" ", parts);
    }

    // Inline-safe HTML tag strip: <[^>]+> → empty (Phase 3 only, no nested angle brackets in templates).
    private static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var sb = new System.Text.StringBuilder(html.Length);
        bool inTag = false;
        foreach (char c in html)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }
}
