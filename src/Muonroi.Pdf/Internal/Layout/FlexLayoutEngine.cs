namespace Muonroi.Pdf.Internal.Layout;

// CSS Flexbox Layout Module Level 1 — the resolution algorithm (FLEX-06).
//
// Mirrors TableLayoutEngine's role: a non-block engine driven from BlockLayoutEngine.DispatchLayout,
// emitting PositionedElements and recursing each item through the EXISTING dispatch
// (_blockEngine.Layout) so nested flex/block/inline/table compose — no layout reimplementation.
//
// First-cut discretion (D-05), documented in 18-03-SUMMARY.md:
//  - baseline alignment is approximated as flex-start (see CrossAxisOffset).
//  - inline-flex (IsInlineFlex) is laid out as an atomic block-level box; no inline integration.
//  - a tall flex container is atomic for pagination (no mid-container page split — see return path).
internal sealed class FlexLayoutEngine
{
    private readonly BlockLayoutEngine _blockEngine;

    internal FlexLayoutEngine(BlockLayoutEngine blockEngine)
    {
        MGuard.NotNull(blockEngine);
        _blockEngine = blockEngine;
    }

    // A flex item after basis resolution and flexible-length distribution.
    private sealed class FlexItem
    {
        public required BoxNode Box { get; init; }
        public float Basis { get; set; }          // resolved flex-basis (main size hint)
        public float MainSize { get; set; }        // final main-axis size after grow/shrink
        public float CrossSize { get; set; }       // measured/assigned cross-axis size
        public float MainStart { get; set; }       // final main-axis offset (relative to container content origin)
        public float CrossStart { get; set; }      // final cross-axis offset (relative to container content origin)
        public bool HasExplicitMain { get; set; }  // item had an explicit main size (Width row / Height column)
        public bool HasExplicitCross { get; set; } // item had an explicit cross size (Height row / Width column)
    }

    private sealed class FlexLine
    {
        public List<FlexItem> Items { get; } = new();
        public float CrossSize { get; set; }       // max item cross size on this line (then possibly stretched)
        public float CrossStart { get; set; }      // cross-axis offset of the line
    }

    public float Layout(FlexContainerBox container, LayoutContext context, List<PositionedElement> output, int pageIndex)
    {
        MGuard.NotNull(container);
        MGuard.NotNull(context);
        MGuard.NotNull(output);

        bool isRow = container.FlexDirection is "row" or "row-reverse";
        bool reverseMain = container.FlexDirection is "row-reverse" or "column-reverse";
        bool wrap = container.FlexWrap is "wrap" or "wrap-reverse";
        bool reverseCross = container.FlexWrap == "wrap-reverse";

        float originX = context.ContentOriginX > 0f ? context.ContentOriginX : context.PageMarginLeftPt;
        float startY = context.CurrentY;

        // Main-axis container size: row → explicit container width when set, else available width;
        // column → explicit height when present, else the remaining page height (generous cap so a
        // column never collapses to 0).
        float containerMain = isRow
            ? (container.Width > 0f ? container.Width : context.AvailableWidth)
            : (container.Height > 0f ? container.Height : MathF.Max(0f, context.RemainingHeight));

        // Main / cross gaps. CSS: column-gap is between items on the main axis for a ROW,
        // row-gap is the main-axis gap for a COLUMN; the other dimension is the cross gap between lines.
        float mainGap = isRow ? container.ColumnGap : container.RowGap;
        float crossGap = isRow ? container.RowGap : container.ColumnGap;

        // Step 1 — order items (ascending Order, stable tiebreak by original index).
        var ordered = container.Children
            .Select((box, idx) => (box, idx))
            .OrderBy(t => t.box.Order ?? 0)
            .ThenBy(t => t.idx)
            .Select(t => t.box)
            .ToList();

        if (ordered.Count == 0)
        {
            context.CurrentY = startY;
            return 0f;
        }

        // Step 2 — resolve each item's flex-basis (main size) + measure cross size.
        var items = new List<FlexItem>(ordered.Count);
        foreach (var box in ordered)
            items.Add(ResolveItem(box, container, isRow, containerMain, context, originX, startY));

        // Step 3 — break into lines.
        var lines = BuildLines(items, wrap, containerMain, mainGap);

        // Step 4 — resolve flexible lengths per line (frozen-item iteration).
        foreach (var line in lines)
            ResolveFlexibleLengths(line, containerMain, mainGap);

        // Step 5 — cross size per line (max item cross), then align-content distribution.
        foreach (var line in lines)
        {
            float maxCross = 0f;
            foreach (var it in line.Items)
                maxCross = MathF.Max(maxCross, it.CrossSize);
            line.CrossSize = maxCross;
        }

        // Container cross size: row → sum of line cross sizes (content-driven height unless explicit);
        // column → available width (the cross axis of a column is horizontal).
        float totalLineCross = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            totalLineCross += lines[i].CrossSize;
            if (i < lines.Count - 1) totalLineCross += crossGap;
        }

        float containerCross = isRow
            ? (container.Height > 0f ? container.Height : totalLineCross)
            : context.AvailableWidth;

        ApplyAlignContent(lines, container.AlignContent, containerCross, totalLineCross, crossGap, reverseCross);

        // Step 6 — per line: justify main axis + align items on cross axis.
        foreach (var line in lines)
        {
            MainAxisPositions(line, container.JustifyContent, containerMain, mainGap, reverseMain);
            foreach (var it in line.Items)
                it.CrossStart = line.CrossStart + CrossAxisOffset(it, line, container);
        }

        // Step 7 — emit each item: set final box main/cross size, build item context, recurse.
        foreach (var line in lines)
        foreach (var it in line.Items)
            EmitItem(it, isRow, originX, startY, context, output, pageIndex);

        // Step 8 — container height consumed.
        // D-05: tall flex container is atomic for pagination (no mid-container page split this phase).
        float totalHeight = isRow ? containerCross : containerMain;
        context.CurrentY = startY + totalHeight;
        return totalHeight;
    }

    // Resolve flex-basis (main size) and cross size for one item.
    private FlexItem ResolveItem(BoxNode box, FlexContainerBox container, bool isRow,
        float containerMain, LayoutContext context, float originX, float startY)
    {
        float explicitMain = isRow ? box.Width : box.Height;   // -1f when not set
        float explicitCross = isRow ? box.Height : box.Width;  // -1f when not set
        bool hasExplicitMain = explicitMain > 0f;
        bool hasExplicitCross = explicitCross > 0f;

        float basis = ResolveBasis(box, isRow, containerMain, explicitMain, context, originX, startY,
            out float measuredCross, out float measuredMain);

        // Cross size: explicit cross when set; else measured content cross.
        float crossSize = hasExplicitCross ? explicitCross : measuredCross;

        // If main basis still unknown after explicit/raw and no measurement happened, fall back.
        if (basis < 0f)
            basis = measuredMain >= 0f ? measuredMain : 0f;

        return new FlexItem
        {
            Box = box,
            Basis = MathF.Max(0f, basis),
            MainSize = MathF.Max(0f, basis),
            CrossSize = MathF.Max(0f, crossSize),
            HasExplicitMain = hasExplicitMain,
            HasExplicitCross = hasExplicitCross,
        };
    }

    // flex-basis resolution. Returns the resolved basis (>=0) or -1f when it must be derived
    // from measurement. Also outputs the measured cross size and measured main size from the
    // content-measurement pass (used for cross sizing and the auto/content basis path).
    private float ResolveBasis(BoxNode box, bool isRow, float containerMain, float explicitMain,
        LayoutContext context, float originX, float startY, out float measuredCross, out float measuredMain)
    {
        measuredCross = 0f;
        measuredMain = -1f;

        string? raw = box.FlexBasisRaw;
        bool basisIsAuto = raw is null or "auto" or "content";

        // Explicit flex-basis length (px/pt/%/...) takes priority over width/height.
        if (!basisIsAuto && raw is { } rawLen)
        {
            float parsed = ParseMainLength(rawLen, containerMain);
            if (parsed >= 0f)
            {
                // Still need a cross measurement for non-explicit cross items.
                MeasureContent(box, isRow, containerMain, context, originX, startY, out measuredCross, out _);
                return parsed;
            }
        }

        // auto/content (or unparseable): use the explicit main size when present, else measure content.
        MeasureContent(box, isRow, containerMain, context, originX, startY, out measuredCross, out measuredMain);

        if (explicitMain > 0f)
            return explicitMain;

        // No explicit main size and basis auto/content → use the measured intrinsic main size.
        return measuredMain;
    }

    // Lay the item out into a throwaway output list to obtain its intrinsic content size.
    //
    // ROW main-axis content width (the path explicit-width tests cannot catch): a CONCRETE
    // max-content pass — lay the item out at a generous AvailableWidth and take the maximum
    // emitted right-edge X minus the origin as the intrinsic width.
    // COLUMN main-axis content size is the measured content height (the Layout return value).
    private void MeasureContent(BoxNode box, bool isRow, float containerMain,
        LayoutContext context, float originX, float startY, out float crossSize, out float mainSize)
    {
        // Generous main size for the measurement pass so content is not artificially wrapped.
        float measureWidth = isRow
            ? (containerMain > 0f ? containerMain : context.AvailableWidth)
            : context.AvailableWidth;
        if (measureWidth <= 0f) measureWidth = context.AvailableWidth > 0f ? context.AvailableWidth : 1000f;

        var measureCtx = new LayoutContext
        {
            PageWidth = context.PageWidth,
            PageHeight = context.PageHeight,
            AvailableWidth = measureWidth,
            CurrentY = startY,
            CurrentPageIndex = context.CurrentPageIndex,
            TotalPages = context.TotalPages,
            TextMetrics = context.TextMetrics,
            PageMargins = context.PageMargins,
            ContentOriginX = originX,
            TextAlign = box.TextAlign ?? context.TextAlign,
        };

        var measureOut = new List<PositionedElement>();
        // Save/restore WidthRaw so the % is not double-applied across measure/place passes (T-18-06).
        var savedWidthRaw = box.WidthRaw;
        float measuredHeight = _blockEngine.Layout(box, measureCtx, measureOut, 0);
        box.WidthRaw = savedWidthRaw;

        // Intrinsic main width (row): max emitted right-edge X − originX (max-content pass).
        float maxRight = originX;
        foreach (var pe in measureOut)
            maxRight = MathF.Max(maxRight, pe.Position.X + pe.Position.Width);
        float intrinsicWidth = MathF.Max(0f, maxRight - originX);

        if (isRow)
        {
            crossSize = measuredHeight;       // cross of a row is vertical → measured height
            mainSize = intrinsicWidth;        // main of a row is horizontal → max-content width
        }
        else
        {
            crossSize = intrinsicWidth;       // cross of a column is horizontal → intrinsic width
            mainSize = measuredHeight;        // main of a column is vertical → measured height
        }
    }

    // Group items into lines. nowrap → single line; wrap → start a new line when the running
    // main size + gap would exceed the container main size.
    private static List<FlexLine> BuildLines(List<FlexItem> items, bool wrap, float containerMain, float mainGap)
    {
        var lines = new List<FlexLine>();
        var current = new FlexLine();
        float used = 0f;

        foreach (var it in items)
        {
            float add = it.Basis + (current.Items.Count > 0 ? mainGap : 0f);
            if (wrap && current.Items.Count > 0 && used + add > containerMain + 0.01f)
            {
                lines.Add(current);
                current = new FlexLine();
                used = 0f;
                add = it.Basis;
            }
            current.Items.Add(it);
            used += add;
        }
        if (current.Items.Count > 0) lines.Add(current);
        if (lines.Count == 0) lines.Add(current);
        return lines;
    }

    // Frozen-item iteration: distribute free space along the main axis.
    // free>0 → grow by flex-grow; free<0 → shrink by flex-shrink scaled by basis. min clamp = 0.
    // T-18-05: bounded — each pass freezes at least one item, so the loop runs ≤ item count.
    private static void ResolveFlexibleLengths(FlexLine line, float containerMain, float mainGap)
    {
        int n = line.Items.Count;
        if (n == 0) return;

        float totalGaps = mainGap * (n - 1);
        float sumBasis = 0f;
        foreach (var it in line.Items)
        {
            it.MainSize = it.Basis;
            sumBasis += it.Basis;
        }

        float free = containerMain - sumBasis - totalGaps;
        if (MathF.Abs(free) < 0.001f) return;

        bool grow = free > 0f;
        var frozen = new bool[n];

        // Bound iterations to n (each pass freezes ≥1 item or terminates).
        for (int pass = 0; pass < n; pass++)
        {
            float remainingFree = containerMain - totalGaps;
            float sumFactor = 0f;
            for (int i = 0; i < n; i++)
            {
                if (frozen[i]) { remainingFree -= line.Items[i].MainSize; continue; }
                remainingFree -= line.Items[i].Basis;
                float grow_i = line.Items[i].Box.FlexGrow ?? 0f;
                float shrink_i = line.Items[i].Box.FlexShrink ?? 1f;
                sumFactor += grow ? grow_i : shrink_i * line.Items[i].Basis;
            }

            if (MathF.Abs(remainingFree) < 0.001f || sumFactor <= 0f)
                break;

            bool anyFrozen = false;
            for (int i = 0; i < n; i++)
            {
                if (frozen[i]) continue;
                float factor = grow
                    ? (line.Items[i].Box.FlexGrow ?? 0f)
                    : (line.Items[i].Box.FlexShrink ?? 1f) * line.Items[i].Basis;

                float delta = sumFactor > 0f ? remainingFree * (factor / sumFactor) : 0f;
                float target = line.Items[i].Basis + delta;

                // min clamp at 0 — freeze any item that would go negative.
                if (target < 0f)
                {
                    target = 0f;
                    frozen[i] = true;
                    anyFrozen = true;
                }
                line.Items[i].MainSize = target;
            }

            if (!anyFrozen) break; // converged — no further clamping needed
        }
    }

    // justify-content: position items along the main axis using leftover free space.
    // Gaps are applied in addition to justify spacing.
    private static void MainAxisPositions(FlexLine line, string justify, float containerMain,
        float mainGap, bool reverseMain)
    {
        int n = line.Items.Count;
        if (n == 0) return;

        float sumMain = 0f;
        foreach (var it in line.Items) sumMain += it.MainSize;
        float totalGaps = mainGap * (n - 1);
        float free = containerMain - sumMain - totalGaps;
        if (free < 0f) free = 0f;

        float leading = 0f;
        float between = 0f;
        switch (justify)
        {
            case "flex-end":      leading = free; break;
            case "center":        leading = free / 2f; break;
            case "space-between": between = n > 1 ? free / (n - 1) : 0f; break;
            case "space-around":  between = free / n; leading = between / 2f; break;
            case "space-evenly":  between = free / (n + 1); leading = between; break;
            default:              leading = 0f; break; // flex-start
        }

        float cursor = leading;
        for (int i = 0; i < n; i++)
        {
            line.Items[i].MainStart = cursor;
            cursor += line.Items[i].MainSize + mainGap + between;
        }

        // row-reverse / column-reverse: mirror the placement within the container main size.
        if (reverseMain)
        {
            foreach (var it in line.Items)
                it.MainStart = containerMain - it.MainStart - it.MainSize;
        }
    }

    // Cross-axis offset of an item within its line per align-items / align-self.
    private static float CrossAxisOffset(FlexItem it, FlexLine line, FlexContainerBox container)
    {
        string align = it.Box.AlignSelf is { } self && self != "auto" ? self : container.AlignItems;
        float room = MathF.Max(0f, line.CrossSize - it.CrossSize);

        switch (align)
        {
            case "flex-end":
                return room;
            case "center":
                return room / 2f;
            case "stretch":
                // Stretch: item with no explicit cross size grows to the line cross size.
                if (!it.HasExplicitCross)
                    it.CrossSize = line.CrossSize;
                return 0f;
            case "baseline":
                // D-05: baseline alignment approximated as flex-start (deferred).
                return 0f;
            default: // flex-start
                return 0f;
        }
    }

    // align-content: distribute lines along the cross axis (multi-line). Single line → fill / start.
    private static void ApplyAlignContent(List<FlexLine> lines, string alignContent,
        float containerCross, float totalLineCross, float crossGap, bool reverseCross)
    {
        int n = lines.Count;
        float free = containerCross - totalLineCross;
        if (free < 0f) free = 0f;

        float leading = 0f;
        float between = 0f;

        if (n <= 1)
        {
            // Single line: stretch fills the container cross; otherwise top-aligned.
            if (alignContent == "stretch" && n == 1)
                lines[0].CrossSize += free;
        }
        else
        {
            switch (alignContent)
            {
                case "flex-end":      leading = free; break;
                case "center":        leading = free / 2f; break;
                case "space-between": between = free / (n - 1); break;
                case "space-around":  between = free / n; leading = between / 2f; break;
                case "stretch":
                    float add = free / n;
                    foreach (var line in lines) line.CrossSize += add;
                    break;
                default:              leading = 0f; break; // flex-start
            }
        }

        // Assign cross start offsets line by line.
        var seq = reverseCross ? Enumerable.Reverse(lines).ToList() : lines;
        float cursor = leading;
        foreach (var line in seq)
        {
            line.CrossStart = cursor;
            cursor += line.CrossSize + crossGap + between;
        }
    }

    // Translate the item into final coordinates and recurse via the existing dispatch.
    private void EmitItem(FlexItem it, bool isRow, float originX, float startY,
        LayoutContext context, List<PositionedElement> output, int pageIndex)
    {
        var box = it.Box;

        // Map main/cross to (X, Y, width, height).
        float itemX, itemY, itemWidth, itemHeight;
        if (isRow)
        {
            itemX = originX + it.MainStart;
            itemY = startY + it.CrossStart;
            itemWidth = it.MainSize;
            itemHeight = it.CrossSize;
        }
        else
        {
            itemX = originX + it.CrossStart;
            itemY = startY + it.MainStart;
            itemWidth = it.CrossSize;
            itemHeight = it.MainSize;
        }

        // Set the solver-resolved sizes on the box so the recursive layout honors grow/shrink/stretch.
        // T-18-06: save/restore WidthRaw so % is not double-applied across passes.
        var savedWidthRaw = box.WidthRaw;
        float savedWidth = box.Width;
        float savedHeight = box.Height;
        box.Width = itemWidth;
        box.WidthRaw = null;
        box.Height = itemHeight;

        var itemCtx = new LayoutContext
        {
            PageWidth = context.PageWidth,
            PageHeight = context.PageHeight,
            AvailableWidth = itemWidth,
            CurrentY = itemY,
            CurrentPageIndex = context.CurrentPageIndex,
            TotalPages = context.TotalPages,
            TextMetrics = context.TextMetrics,
            PageMargins = context.PageMargins,
            ContentOriginX = itemX,
            ContainingBlockRect = new Rect(itemX, itemY, itemWidth, itemHeight),
            TextAlign = box.TextAlign ?? context.TextAlign,
        };

        var itemOut = new List<PositionedElement>();
        // Recurse through the EXISTING dispatch so nested flex/block/inline/table compose.
        _blockEngine.Layout(box, itemCtx, itemOut, pageIndex);

        box.WidthRaw = savedWidthRaw;
        box.Width = savedWidth;
        box.Height = savedHeight;

        output.Add(new PositionedElement
        {
            Source = box,
            Position = new Rect(itemX, itemY, itemWidth, itemHeight),
            PageIndex = pageIndex
        });
        output.AddRange(itemOut);
    }

    // Parse a main-axis CSS length (px/pt/mm/cm/in/%) into points. % resolves against containerMain.
    // Returns -1f when the value is not a resolvable length.
    private static float ParseMainLength(string raw, float containerMain)
    {
        ReadOnlySpan<char> span = raw.AsSpan().Trim();
        if (span.IsEmpty) return -1f;

        if (span.EndsWith("%", StringComparison.Ordinal))
            return TryNum(span[..^1], out float pct) ? containerMain * pct / 100f : -1f;
        if (span.EndsWith("px", StringComparison.Ordinal))
            return TryNum(span[..^2], out float px) ? px * (float)Units.PxToPt : -1f;
        if (span.EndsWith("pt", StringComparison.Ordinal))
            return TryNum(span[..^2], out float pt) ? pt : -1f;
        if (span.EndsWith("mm", StringComparison.Ordinal))
            return TryNum(span[..^2], out float mm) ? mm * (float)Units.MmToPt : -1f;
        if (span.EndsWith("cm", StringComparison.Ordinal))
            return TryNum(span[..^2], out float cm) ? cm * (float)Units.CmToPt : -1f;
        if (span.EndsWith("in", StringComparison.Ordinal))
            return TryNum(span[..^2], out float inch) ? inch * Units.InToPt : -1f;
        return TryNum(span, out float bare) ? bare * (float)Units.PxToPt : -1f; // bare number → px
    }

    private static bool TryNum(ReadOnlySpan<char> span, out float value) =>
        float.TryParse(span.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
}
