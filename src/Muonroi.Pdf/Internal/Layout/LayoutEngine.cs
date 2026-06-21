using System.Diagnostics.CodeAnalysis;
using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Image;
using Muonroi.Pdf.Internal.Layout.Geometry;
using SixLabors.Fonts;

namespace Muonroi.Pdf.Internal.Layout;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfInputLimitException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy.")]

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
        var flexEngine = new FlexLayoutEngine(_blockEngine);
        _blockEngine.FlexEngine = flexEngine;
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
        bool allowModernLayout,
        IFontResolver? fontResolver,
        IResourceResolver? imageResolver,
        IImageDecoder imageDecoder,
        CancellationToken ct,
        RunningContentSpec? running = null)
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
            // Phase 13: also resolve images referenced by running header/footer fragments.
            IReadOnlyList<IStyledDocument>? runningDocs = CollectRunningDocs(running);
            resolvedImages = await imagePipeline.ResolveAsync(doc, runningDocs, imageResolver, imageDecoder, limits, ct).ConfigureAwait(false);
        }
        else
        {
            resolvedImages = new Dictionary<string, DecodedImage>();
        }

        LayoutEngine engineToUse = fontResolver != null && realMetrics != null
            ? new LayoutEngine(realMetrics)
            : this;

        var pass1 = engineToUse.RunLayout(doc, options, totalPages: 0, resolvedImages, running, allowModernLayout);

        if (pass1.PageCount > PdfConfigs.PdfLimits.Defaults.MaxPages)
            throw new PdfInputLimitException(
                "PAGE-MAX-PAGES",
                "MaxPages",
                pass1.PageCount,
                PdfConfigs.PdfLimits.Defaults.MaxPages);

        ct.ThrowIfCancellationRequested();

        var pass2 = engineToUse.RunLayout(doc, options, totalPages: pass1.PageCount, resolvedImages, running, allowModernLayout);

        var embeddedFonts = new List<EmbeddedFontInfo>();
        if (fontResolver != null && fontBytesMap.Count > 0 && fontCollection != null)
        {
            var collector = new GlyphCollector();
            IReadOnlyDictionary<string, IReadOnlySet<int>> usedCodepoints = collector.Collect(pass2, fontCollection);

            foreach (KeyValuePair<string, ReadOnlyMemory<byte>> kvp in fontBytesMap)
            {
                string family = kvp.Key;

                FontFaceDeclaration? decl = doc.FontFaces.FirstOrDefault(f => f.Family == family);

                if (decl != null)
                {
                    // @font-face path: family name is the CSS @font-face name. GlyphCollector
                    // collects under this same name since InlineBox.FontFamily = CSS family.
                    IReadOnlySet<int> codepoints = usedCodepoints.TryGetValue(family, out IReadOnlySet<int>? cp) ? cp : new HashSet<int>();
                    var subsetter = new TrueTypeFontSubsetter();
                    FontSubsetResult subsetResult = subsetter.Subset(kvp.Value, codepoints);

                    embeddedFonts.Add(new EmbeddedFontInfo(
                        decl.Family, decl.Weight, decl.Style,
                        subsetResult.SubsetBytes, codepoints,
                        subsetResult.OldToNewGid, subsetResult.SortedGids,
                        subsetResult.CpToNewGid));

                    // FONT-ALIAS-01: When a consumer @font-face matches a bundled canonical family
                    // (e.g. "Times New Roman" → Liberation Serif), elements lacking an explicit
                    // font-family still default to the canonical's CSS aliases ("serif"). Without
                    // alias EmbeddedFontInfo entries, cpToNewGidMap["serif"] is empty and the writer
                    // throws "Font GID map missing or empty for family 'serif'". Generate alias
                    // EmbeddedFontInfo entries that share the same bytes but subset against the
                    // codepoints collected under each alias.
                    if (BundledFonts.TryGetFallback(decl.Family, decl.Weight, decl.Style, out _, out string canonical))
                    {
                        foreach (string alias in BundledFonts.GetAliasesForCanonical(canonical))
                        {
                            if (string.Equals(alias, decl.Family, StringComparison.OrdinalIgnoreCase))
                                continue; // already handled above

                            if (!usedCodepoints.TryGetValue(alias, out IReadOnlySet<int>? aliasCp) || aliasCp.Count == 0)
                                continue; // alias not referenced by any inline box

                            var aliasSubsetter = new TrueTypeFontSubsetter();
                            FontSubsetResult aliasSubset = aliasSubsetter.Subset(kvp.Value, aliasCp);

                            embeddedFonts.Add(new EmbeddedFontInfo(
                                alias, decl.Weight, decl.Style,
                                aliasSubset.SubsetBytes, aliasCp,
                                aliasSubset.OldToNewGid, aliasSubset.SortedGids,
                                aliasSubset.CpToNewGid));
                        }
                    }
                }
                else
                {
                    // No @font-face declaration. The family may be a canonical bundled font name
                    // (e.g. "Liberation Serif") registered by FontPipeline as a fallback.
                    // GlyphCollector collects under the CSS alias names that InlineBox uses
                    // (e.g. "Times New Roman", "serif"), NOT under the canonical name.
                    // For each CSS alias that was actually used (has collected codepoints), produce
                    // a separate EmbeddedFontInfo keyed by the alias so OwnedPdfWriter's
                    // cpToNewGidMap lookup by inline.FontFamily resolves correctly.
                    // Bundled fonts are always embeddable — @font-face is NOT a precondition.
                    string[] aliases = BundledFonts.GetAliasesForCanonical(family);
                    if (aliases.Length == 0)
                    {
                        // Not a bundled font and no @font-face — skip (font bytes came from
                        // fontResolver but no declaration exists to describe it; should not occur
                        // in practice but guard defensively).
                        continue;
                    }

                    foreach (string alias in aliases)
                    {
                        if (!usedCodepoints.TryGetValue(alias, out IReadOnlySet<int>? aliasCodepoints)
                            || aliasCodepoints.Count == 0)
                        {
                            // This alias was not referenced by any inline box; skip.
                            continue;
                        }

                        var subsetter = new TrueTypeFontSubsetter();
                        FontSubsetResult subsetResult = subsetter.Subset(kvp.Value, aliasCodepoints);

                        embeddedFonts.Add(new EmbeddedFontInfo(
                            alias,
                            Muonroi.Pdf.Abstractions.FontWeight.Normal,
                            Muonroi.Pdf.Abstractions.FontStyle.Normal,
                            subsetResult.SubsetBytes, aliasCodepoints,
                            subsetResult.OldToNewGid, subsetResult.SortedGids,
                            subsetResult.CpToNewGid));
                    }
                }
            }
        }

        pass2.EmbeddedFonts = embeddedFonts;
        pass2.Images = resolvedImages;

        return pass2;
    }

    private PositionedPageList RunLayout(IStyledDocument doc, PdfRenderOptions options, int totalPages, IReadOnlyDictionary<string, DecodedImage>? resolvedImages = null, RunningContentSpec? running = null, bool allowModernLayout = false)
    {
        var (pageWidthPt, pageHeightPt) = GetPageDimensions(options);
        var margins = ResolveMargins(options, doc.PageRule);

        float topMarginPt = (float)(margins.TopMm * Units.MmToPt);
        float bottomMarginPt = (float)(margins.BottomMm * Units.MmToPt);
        float leftMarginPt = (float)(margins.LeftMm * Units.MmToPt);
        float rightMarginPt = (float)(margins.RightMm * Units.MmToPt);

        // Phase 13: render running header/footer columns first. Their measured band heights can
        // expand the effective margins — body is pushed below the header band / above the footer
        // band so the two never overlap (locked decision: HeightMm grows the margin).
        RenderedRunningContent? rc = BuildRunningContent(
            running, pageWidthPt, pageHeightPt, leftMarginPt, rightMarginPt, totalPages, resolvedImages);

        float effectiveTopPt = topMarginPt;
        float effectiveBottomPt = bottomMarginPt;
        if (rc is not null)
        {
            if (rc.HeaderBandPt > effectiveTopPt) effectiveTopPt = rc.HeaderBandPt;
            if (rc.FooterBandPt > effectiveBottomPt) effectiveBottomPt = rc.FooterBandPt;
        }

        float pageBodyHeight = pageHeightPt - effectiveTopPt - effectiveBottomPt;
        float availableWidth = pageWidthPt - leftMarginPt - rightMarginPt;

        var rootBox = _boxTreeBuilder.Build(doc.Root, resolvedImages, allowModernLayout);

        var context = new LayoutContext
        {
            PageWidth = pageWidthPt,
            PageHeight = pageHeightPt,
            AvailableWidth = availableWidth,
            // Body content is laid out in 0-based continuous body-space; PaginationEngine adds the
            // top margin per physical page via localY. Starting at topMarginPt double-counted the
            // top margin (content pushed down one margin on page 0, and tall single-page content
            // spuriously broke to page 2 — the G8 blank-first-page bug).
            CurrentY = 0f,
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
            effectiveTopPt,
            effectiveBottomPt,
            pageWidthPt,
            pageHeightPt,
            totalPages,
            rc);
    }

    // Phase 13: lay out each header/footer column fragment with the SAME text metrics as the body,
    // offset into its column third, and measure the tallest band. Returns null when no running
    // content is configured. The elements are stamped per page by PaginationEngine.
    private static IReadOnlyList<IStyledDocument>? CollectRunningDocs(RunningContentSpec? spec)
    {
        if (spec is null) return null;
        var docs = new List<IStyledDocument>(6);
        void Add(IStyledDocument? d) { if (d is not null) docs.Add(d); }
        Add(spec.HeaderLeft); Add(spec.HeaderCenter); Add(spec.HeaderRight);
        Add(spec.FooterLeft); Add(spec.FooterCenter); Add(spec.FooterRight);
        return docs.Count > 0 ? docs : null;
    }

    private RenderedRunningContent? BuildRunningContent(
        RunningContentSpec? spec,
        float pageWidthPt,
        float pageHeightPt,
        float leftMarginPt,
        float rightMarginPt,
        int totalPages,
        IReadOnlyDictionary<string, DecodedImage>? resolvedImages)
    {
        _ = pageHeightPt;
        if (spec is null || (!spec.HasHeader && !spec.HasFooter)) return null;

        float contentWidth = pageWidthPt - leftMarginPt - rightMarginPt;
        if (contentWidth <= 0f) contentWidth = pageWidthPt;
        float colWidth = contentWidth / 3f;

        var rc = new RenderedRunningContent
        {
            HeaderShowLine = spec.HeaderShowLine,
            FooterShowLine = spec.FooterShowLine,
            LineColor = spec.LineColor,
            ContentLeftPt = leftMarginPt,
            ContentWidthPt = contentWidth,
        };

        float xLeft = leftMarginPt;
        float xCenter = leftMarginPt + colWidth;
        float xRight = leftMarginPt + 2f * colWidth;

        float hMax = 0f;
        hMax = Math.Max(hMax, RenderColumnInto(rc.HeaderElements, spec.HeaderLeft, xLeft, colWidth, pageWidthPt, totalPages, resolvedImages));
        hMax = Math.Max(hMax, RenderColumnInto(rc.HeaderElements, spec.HeaderCenter, xCenter, colWidth, pageWidthPt, totalPages, resolvedImages));
        hMax = Math.Max(hMax, RenderColumnInto(rc.HeaderElements, spec.HeaderRight, xRight, colWidth, pageWidthPt, totalPages, resolvedImages));
        rc.HeaderBandPt = Math.Max(spec.HeaderHeightPt, hMax);

        float fMax = 0f;
        fMax = Math.Max(fMax, RenderColumnInto(rc.FooterElements, spec.FooterLeft, xLeft, colWidth, pageWidthPt, totalPages, resolvedImages));
        fMax = Math.Max(fMax, RenderColumnInto(rc.FooterElements, spec.FooterCenter, xCenter, colWidth, pageWidthPt, totalPages, resolvedImages));
        fMax = Math.Max(fMax, RenderColumnInto(rc.FooterElements, spec.FooterRight, xRight, colWidth, pageWidthPt, totalPages, resolvedImages));
        rc.FooterBandPt = Math.Max(spec.FooterHeightPt, fMax);

        return rc;
    }

    private float RenderColumnInto(
        List<PositionedElement> dest,
        IStyledDocument? colDoc,
        float xOffset,
        float colWidth,
        float pageWidthPt,
        int totalPages,
        IReadOnlyDictionary<string, DecodedImage>? resolvedImages)
    {
        if (colDoc is null) return 0f;

        // First-cut deferral (Phase 18): running header/footer columns never enable modern layout.
        // Flex inside @page running content degrades to block (allowModernLayout: false). Running
        // content has no flex use-case today; documented in 18-02-SUMMARY.md.
        var rootBox = _boxTreeBuilder.Build(colDoc.Root, resolvedImages, allowModernLayout: false);
        var ctx = new LayoutContext
        {
            PageWidth = pageWidthPt,
            PageHeight = 100000f, // effectively unbounded — running content never paginates
            AvailableWidth = colWidth,
            CurrentY = 0f,
            CurrentPageIndex = 0,
            TotalPages = totalPages,
            TextMetrics = _textMetrics,
            PageMargins = PdfMargins.Zero,
        };

        var els = new List<PositionedElement>();
        _blockEngine.Layout(rootBox, ctx, els, 0, isRoot: true);

        float height = 0f;
        foreach (var e in els)
        {
            e.Position = new Rect(e.Position.X + xOffset, e.Position.Y, e.Position.Width, e.Position.Height);
            dest.Add(e);
            float bottom = e.Position.Y + e.Position.Height;
            if (bottom > height) height = bottom;
        }
        return height;
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
