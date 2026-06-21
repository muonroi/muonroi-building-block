using Muonroi.Core.Abstractions.Guards;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

// CSS Grid Layout Module Level 1/2 — the track-sizing + placement essentials (GRID-05).
//
// Mirrors FlexLayoutEngine/TableLayoutEngine's role: a non-block engine driven from
// BlockLayoutEngine.DispatchLayout, emitting PositionedElements and recursing each item through the
// EXISTING dispatch (_blockEngine.Layout) so nested grid/flex/block/inline/table compose — no
// layout reimplementation.
//
// The genuinely-new algorithm relative to flex is 2-D: per-axis track sizing (fixed → auto/content
// → fr distribution with minmax clamps) on BOTH axes, three placement modes (explicit line numbers
// / named areas / sparse auto-flow), and cell-rect positioning with justify/align items/self/content.
//
// First-cut discretion (D-01), documented in 19-03-SUMMARY.md:
//  - auto-fill/auto-fit not supported (repeat() is expanded with a fixed count at parse time).
//  - grid-auto-flow dense not supported — sparse packing only.
//  - subgrid / masonry not supported.
//  - baseline alignment approximated as start.
//  - a percentage track/gap against an indefinite container is treated as auto/content.
//  - a tall grid container is atomic for pagination (no mid-container page split).
//  - inline-grid (IsInlineGrid) is laid out identically as an atomic block-level box.
internal sealed class GridLayoutEngine
{
    private readonly BlockLayoutEngine _blockEngine;

    internal GridLayoutEngine(BlockLayoutEngine blockEngine)
    {
        MGuard.NotNull(blockEngine);
        _blockEngine = blockEngine;
    }

    // Resolved 1-based placement of one item over the implicit+explicit track grid (0-based indices here).
    private sealed class GridPlacement
    {
        public required BoxNode Box { get; init; }
        public int RowStart { get; set; }   // 0-based row index
        public int RowSpan { get; set; } = 1;
        public int ColStart { get; set; }   // 0-based column index
        public int ColSpan { get; set; } = 1;
    }

    public float Layout(GridContainerBox container, LayoutContext context, List<PositionedElement> output, int pageIndex)
    {
        MGuard.NotNull(container);
        MGuard.NotNull(context);
        MGuard.NotNull(output);

        float originX = context.ContentOriginX > 0f ? context.ContentOriginX : context.PageMarginLeftPt;
        float startY = context.CurrentY;
        float containerWidth = container.Width > 0f ? container.Width : context.AvailableWidth;
        float containerHeight = container.Height > 0f ? container.Height : -1f; // resolved after row sizing when not explicit

        var children = container.Children;
        if (children.Count == 0)
        {
            context.CurrentY = startY;
            return containerHeight > 0f ? containerHeight : 0f;
        }

        // STEP 1 — PLACEMENT. Resolve explicit / named-area placements, then sparse auto-flow the rest.
        // Produces a placement per child plus the final (explicit+implicit) column/row counts.
        var placements = PlaceItems(container, out int colCount, out int rowCount);

        // STEP 2 — build the effective track lists (explicit template + implicit tracks sized by
        // grid-auto-columns / grid-auto-rows, defaulting to auto).
        var colTracks = BuildEffectiveTracks(container.TemplateColumns, container.AutoColumns, colCount);
        var rowTracks = BuildEffectiveTracks(container.TemplateRows, container.AutoRows, rowCount);

        // STEP 3 — COLUMN sizing against the (definite) container width.
        float colGap = container.ColumnGap;
        float[] colSizes = ResolveTrackSizes(
            colTracks, containerWidth, definiteAxis: true, colGap,
            placements, isColumnAxis: true, container, context, originX, startY,
            colSizesForRowMeasure: null);
        float[] colOffsets = CumulativeOffsets(colSizes, colGap);

        // STEP 4 — ROW sizing. Container height is content-driven when not explicit (indefinite).
        float rowGap = container.RowGap;
        float[] rowSizes = ResolveTrackSizes(
            rowTracks, containerHeight, definiteAxis: containerHeight > 0f, rowGap,
            placements, isColumnAxis: false, container, context, originX, startY,
            colSizesForRowMeasure: colSizes);
        float[] rowOffsets = CumulativeOffsets(rowSizes, rowGap);

        // Total track extents (including interior gaps).
        float colsExtent = TracksExtent(colSizes, colGap);
        float rowsExtent = TracksExtent(rowSizes, rowGap);

        // STEP 5 — CONTENT-GROUP alignment: offset the whole track group when it does not fill the
        // container along an axis (justify-content / align-content). Indefinite row axis → no offset.
        float groupOffsetX = ApplyContentAlignment(container.JustifyContent, containerWidth, colsExtent);
        float definiteHeight = containerHeight > 0f ? containerHeight : rowsExtent;
        float groupOffsetY = ApplyContentAlignment(container.AlignContent, definiteHeight, rowsExtent);

        // STEP 6 — EMIT each placed item into its cell rect, applying self alignment, recursing.
        foreach (var p in placements)
        {
            float cellX = originX + colOffsets[p.ColStart] + groupOffsetX;
            float cellY = startY + rowOffsets[p.RowStart] + groupOffsetY;
            float cellW = SpanSize(colSizes, colGap, p.ColStart, p.ColSpan);
            float cellH = SpanSize(rowSizes, rowGap, p.RowStart, p.RowSpan);

            EmitItem(p, container, cellX, cellY, cellW, cellH, context, output, pageIndex);
        }

        // STEP 7 — container height consumed.
        // D-01: tall grid container is atomic for pagination (no mid-container page split this phase).
        float totalHeight = containerHeight > 0f ? containerHeight : rowsExtent + groupOffsetY;
        context.CurrentY = startY + totalHeight;
        return totalHeight;
    }

    // ---- PLACEMENT ---------------------------------------------------------------------------

    // Resolve every child's row/col placement. Explicit (line numbers / span / negative-from-end /
    // grid-area shorthand) and named-area items are placed first; remaining items flow sparsely per
    // grid-auto-flow into the next free cell. Implicit tracks are created beyond the explicit
    // template, BOUNDED by the item count (T-19-06: each unplaced item consumes at most one cell).
    private static List<GridPlacement> PlaceItems(GridContainerBox container, out int colCount, out int rowCount)
    {
        int explicitCols = Math.Max(container.TemplateColumns.Count, 1);
        int explicitRows = Math.Max(container.TemplateRows.Count, 1);

        var areaIndex = BuildAreaIndex(container.TemplateAreas);

        var placements = new List<GridPlacement>(container.Children.Count);
        var explicitlyPlaced = new List<GridPlacement>();
        var autoItems = new List<BoxNode>();

        int maxCol = explicitCols;
        int maxRow = explicitRows;

        // Pass A — resolve explicit + named-area placements.
        foreach (var child in container.Children)
        {
            var p = ResolveExplicit(child, container, areaIndex, explicitCols, explicitRows);
            if (p == null)
            {
                autoItems.Add(child);
                continue;
            }
            explicitlyPlaced.Add(p);
            placements.Add(p);
            maxCol = Math.Max(maxCol, p.ColStart + p.ColSpan);
            maxRow = Math.Max(maxRow, p.RowStart + p.RowSpan);
        }

        // Occupancy grid for sparse auto-placement. Bound dimensions by item count (T-19-06).
        bool flowColumn = string.Equals(container.AutoFlow, "column", StringComparison.OrdinalIgnoreCase);
        int n = container.Children.Count;
        // The fixed axis (columns for row-flow, rows for column-flow) is the explicit track count.
        int fixedAxis = flowColumn ? explicitRows : explicitCols;
        if (fixedAxis < 1) fixedAxis = 1;

        var occupied = new HashSet<long>();
        foreach (var p in explicitlyPlaced)
            for (int r = p.RowStart; r < p.RowStart + p.RowSpan; r++)
                for (int c = p.ColStart; c < p.ColStart + p.ColSpan; c++)
                    occupied.Add(CellKey(r, c));

        // Sparse cursor: walk cells in flow order, skipping occupied cells, creating implicit tracks
        // along the flow axis as needed (bounded by item count).
        int cursorMain = 0; // index along the flow axis (column for row-flow → that's the column; for column-flow → the row)
        int cursorCross = 0; // index along the fixed (cross) axis
        foreach (var child in autoItems)
        {
            int span = flowColumn
                ? ResolveSpanOnly(child.GridRowRaw)   // column-flow: span affects the flow axis (rows)
                : ResolveSpanOnly(child.GridColumnRaw); // row-flow: span affects the flow axis (columns)
            if (span < 1) span = 1;
            if (span > fixedAxis) span = fixedAxis; // a span cannot exceed the fixed axis count

            // Advance the cursor to the next free run of `span` cells in flow order.
            // T-19-06: bounded — the flow axis grows at most by the item count.
            while (true)
            {
                // Wrap on the cross axis when the span would overflow the fixed axis.
                if (cursorCross + span > fixedAxis)
                {
                    cursorCross = 0;
                    cursorMain++;
                }

                bool free = true;
                for (int k = 0; k < span; k++)
                {
                    int r = flowColumn ? cursorCross + k : cursorMain;
                    int c = flowColumn ? cursorMain : cursorCross + k;
                    if (occupied.Contains(CellKey(r, c))) { free = false; break; }
                }

                if (free) break;
                cursorCross++;
            }

            int rowStart, colStart, rowSpan, colSpan;
            if (flowColumn)
            {
                rowStart = cursorCross; rowSpan = span;
                colStart = cursorMain; colSpan = 1;
            }
            else
            {
                colStart = cursorCross; colSpan = span;
                rowStart = cursorMain; rowSpan = 1;
            }

            var p = new GridPlacement { Box = child, RowStart = rowStart, RowSpan = rowSpan, ColStart = colStart, ColSpan = colSpan };
            placements.Add(p);
            for (int k = 0; k < span; k++)
            {
                int r = flowColumn ? cursorCross + k : cursorMain;
                int c = flowColumn ? cursorMain : cursorCross + k;
                occupied.Add(CellKey(r, c));
            }
            maxCol = Math.Max(maxCol, colStart + colSpan);
            maxRow = Math.Max(maxRow, rowStart + rowSpan);

            cursorCross += span;
        }

        // Final track counts are bounded by explicit template + at most the item count of implicit tracks.
        colCount = Math.Max(1, Math.Min(maxCol, explicitCols + n));
        rowCount = Math.Max(1, Math.Min(maxRow, explicitRows + n));
        _ = cursorMain;
        return placements;
    }

    // Resolve an explicit placement from grid-area / grid-column / grid-row. Returns null when the
    // item is fully auto-placed (no explicit lines / named area). Malformed tokens → auto (null).
    private static GridPlacement? ResolveExplicit(BoxNode child, GridContainerBox container,
        Dictionary<string, (int RowStart, int RowEnd, int ColStart, int ColEnd)> areaIndex,
        int explicitCols, int explicitRows)
    {
        // 1) grid-area: a single name resolves via grid-template-areas; the 4-value shorthand parses
        //    to row-start / col-start / row-end / col-end.
        string? area = child.GridAreaRaw;
        if (!string.IsNullOrWhiteSpace(area))
        {
            string a = area.Trim();
            if (!a.Contains('/'))
            {
                // Named area.
                if (areaIndex.TryGetValue(a, out var rect))
                {
                    return new GridPlacement
                    {
                        Box = child,
                        RowStart = rect.RowStart,
                        RowSpan = Math.Max(1, rect.RowEnd - rect.RowStart),
                        ColStart = rect.ColStart,
                        ColSpan = Math.Max(1, rect.ColEnd - rect.ColStart),
                    };
                }
                // Unknown named area → auto-place.
                return null;
            }

            // 4-value shorthand: row-start / col-start / row-end / col-end.
            var parts = a.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4)
            {
                ResolveAxisLines(parts[0], parts[2], explicitRows, out int rs, out int rsp);
                ResolveAxisLines(parts[1], parts[3], explicitCols, out int cs, out int csp);
                return new GridPlacement { Box = child, RowStart = rs, RowSpan = rsp, ColStart = cs, ColSpan = csp };
            }
            // Otherwise fall through to grid-column/grid-row.
        }

        string? colRaw = child.GridColumnRaw;
        string? rowRaw = child.GridRowRaw;
        bool hasCol = !string.IsNullOrWhiteSpace(colRaw);
        bool hasRow = !string.IsNullOrWhiteSpace(rowRaw);
        if (!hasCol && !hasRow)
            return null; // fully auto

        int colStart = 0, colSpan = 1, rowStart = 0, rowSpan = 1;
        bool placedCol = false, placedRow = false;

        if (colRaw != null && hasCol)
            placedCol = TryResolveLineSpec(colRaw, explicitCols, out colStart, out colSpan);
        if (rowRaw != null && hasRow)
            placedRow = TryResolveLineSpec(rowRaw, explicitRows, out rowStart, out rowSpan);

        if (!placedCol && !placedRow)
            return null; // both malformed / pure span without a definite start → treat as auto

        // A definite start on one axis with the other auto: anchor the auto axis at 0 (first-cut).
        return new GridPlacement
        {
            Box = child,
            RowStart = placedRow ? rowStart : 0,
            RowSpan = placedRow ? rowSpan : 1,
            ColStart = placedCol ? colStart : 0,
            ColSpan = placedCol ? colSpan : 1,
        };
    }

    // Parse a "grid-column"/"grid-row" value: "N", "A / B", "span K", "N / span K". Resolves
    // 1-based line numbers (negative count from the end) into a 0-based start + span. Returns false
    // when the value yields no definite start (e.g. bare "span K" or "auto") → caller auto-places.
    private static bool TryResolveLineSpec(string raw, int explicitCount, out int start, out int span)
    {
        start = 0; span = 1;
        string v = raw.Trim();
        if (v.Length == 0 || string.Equals(v, "auto", StringComparison.OrdinalIgnoreCase))
            return false;

        string[] sides = v.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int lineCount = explicitCount + 1; // N tracks → N+1 lines

        if (sides.Length == 1)
        {
            // "N" or "span K".
            if (TryParseSpan(sides[0], out int onlySpan))
            {
                // Pure span with no start — not a definite placement.
                span = onlySpan;
                return false;
            }
            if (TryParseLine(sides[0], lineCount, out int line))
            {
                start = line - 1;            // 1-based line → 0-based track index
                if (start < 0) start = 0;
                span = 1;
                return true;
            }
            return false;
        }

        // Two sides: start and end. Either may be "span K".
        ResolveAxisLines(sides[0], sides[1], explicitCount, out start, out span);
        return true;
    }

    // Resolve a (start, end) line pair where either side may be "span K". Outputs 0-based start + span.
    private static void ResolveAxisLines(string startTok, string endTok, int explicitCount, out int start, out int span)
    {
        int lineCount = explicitCount + 1;
        bool startIsSpan = TryParseSpan(startTok, out int startSpan);
        bool endIsSpan = TryParseSpan(endTok, out int endSpan);

        if (!startIsSpan && TryParseLine(startTok, lineCount, out int startLine))
        {
            start = Math.Max(0, startLine - 1);
            if (endIsSpan)
            {
                span = Math.Max(1, endSpan);
            }
            else if (TryParseLine(endTok, lineCount, out int endLine))
            {
                int endIdx = Math.Max(0, endLine - 1);
                span = Math.Max(1, endIdx - start);
            }
            else
            {
                span = 1;
            }
            return;
        }

        // Start is a span (or unparseable) → anchor at 0 with the given span.
        start = 0;
        span = startIsSpan ? Math.Max(1, startSpan) : (endIsSpan ? Math.Max(1, endSpan) : 1);
    }

    // "span K" → K. Returns false when not a span token.
    private static bool TryParseSpan(string token, out int span)
    {
        span = 1;
        string t = token.Trim();
        if (!t.StartsWith("span", StringComparison.OrdinalIgnoreCase))
            return false;
        string rest = t.Substring(4).Trim();
        if (rest.Length == 0) { span = 1; return true; }
        if (int.TryParse(rest, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int k) && k > 0)
            span = k;
        return true;
    }

    // A 1-based line number (negative counts from the end: -1 = last line). Returns false on parse failure.
    private static bool TryParseLine(string token, int lineCount, out int line)
    {
        line = 1;
        if (!int.TryParse(token.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n))
            return false;
        if (n < 0)
            n = lineCount + n + 1; // -1 → lineCount (last line)
        if (n < 1) n = 1;
        line = n;
        return true;
    }

    // Span-only extraction for auto-placement (the flow-axis span of an otherwise-auto item).
    private static int ResolveSpanOnly(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 1;
        string v = raw.Trim();
        // "span K" anywhere, or "A / span K".
        foreach (var side in v.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (TryParseSpan(side, out int s)) return Math.Max(1, s);
        return 1;
    }

    // Build a name → bounding-rect (row/col start..end, 0-based, end exclusive) index from
    // grid-template-areas. "." cells are skipped. Ragged arrays were rejected at parse time.
    private static Dictionary<string, (int RowStart, int RowEnd, int ColStart, int ColEnd)> BuildAreaIndex(string[][] areas)
    {
        var index = new Dictionary<string, (int RowStart, int RowEnd, int ColStart, int ColEnd)>(StringComparer.Ordinal);
        if (areas.Length == 0) return index;

        for (int r = 0; r < areas.Length; r++)
        {
            var rowTokens = areas[r];
            for (int c = 0; c < rowTokens.Length; c++)
            {
                string name = rowTokens[c];
                if (string.IsNullOrEmpty(name) || name == ".")
                    continue;

                if (index.TryGetValue(name, out var rect))
                {
                    index[name] = (
                        Math.Min(rect.RowStart, r),
                        Math.Max(rect.RowEnd, r + 1),
                        Math.Min(rect.ColStart, c),
                        Math.Max(rect.ColEnd, c + 1));
                }
                else
                {
                    index[name] = (r, r + 1, c, c + 1);
                }
            }
        }
        return index;
    }

    private static long CellKey(int row, int col) => ((long)row << 32) | (uint)col;

    // ---- TRACK SIZING ------------------------------------------------------------------------

    // Build the effective track list: the explicit template followed by implicit tracks (sized by
    // the auto template, defaulting to auto) up to `count`. When the template is empty, all tracks
    // are implicit.
    private static List<GridTrack> BuildEffectiveTracks(List<GridTrack> template, GridTrack? autoTrack, int count)
    {
        var tracks = new List<GridTrack>(count);
        for (int i = 0; i < count; i++)
        {
            if (i < template.Count)
                tracks.Add(template[i]);
            else
                tracks.Add(autoTrack ?? new GridTrack { Kind = GridTrackKind.Auto });
        }
        if (tracks.Count == 0)
            tracks.Add(new GridTrack { Kind = GridTrackKind.Auto });
        return tracks;
    }

    // Resolve one axis of tracks into pixel sizes.
    //   1. Fixed (Length) and Percent (× axisSize when definite; auto when indefinite — D-01).
    //   2. Auto/content tracks → max-content of items whose span covers that track.
    //   3. fr tracks split the remaining free space proportionally, honoring minmax clamps.
    private float[] ResolveTrackSizes(
        List<GridTrack> tracks, float axisSize, bool definiteAxis, float gap,
        List<GridPlacement> placements, bool isColumnAxis,
        GridContainerBox container, LayoutContext context, float originX, float startY,
        float[]? colSizesForRowMeasure)
    {
        int n = tracks.Count;
        var sizes = new float[n];
        var isFr = new bool[n];
        var frValue = new float[n];
        var frMax = new float[n]; // minmax max clamp on an fr track (PositiveInfinity = uncapped)

        float totalGaps = gap * Math.Max(0, n - 1);
        float available = definiteAxis ? MathF.Max(0f, axisSize - totalGaps) : -1f;

        // Step 1 + 2 — resolve non-fr tracks.
        for (int i = 0; i < n; i++)
        {
            var t = tracks[i];
            switch (t.Kind)
            {
                case GridTrackKind.Length:
                    sizes[i] = MathF.Max(0f, t.Length);
                    break;

                case GridTrackKind.Percent:
                    // D-01: % against an indefinite axis is treated as auto/content.
                    sizes[i] = definiteAxis ? MathF.Max(0f, t.Percent * axisSize) : MeasureTrack(i, placements, isColumnAxis, container, context, originX, startY, colSizesForRowMeasure);
                    break;

                case GridTrackKind.Fraction:
                    isFr[i] = true;
                    frValue[i] = MathF.Max(0f, t.Fraction);
                    frMax[i] = float.PositiveInfinity;
                    sizes[i] = 0f;
                    break;

                case GridTrackKind.MinMax:
                    ResolveMinMax(t, axisSize, definiteAxis, i, placements, isColumnAxis, container, context, originX, startY, colSizesForRowMeasure,
                        out float resolved, out bool minmaxIsFr, out float minmaxFr, out float minFloor, out float maxCap);
                    if (minmaxIsFr)
                    {
                        isFr[i] = true;
                        frValue[i] = minmaxFr;
                        frMax[i] = maxCap;
                        sizes[i] = MathF.Max(0f, minFloor); // fr floor from min
                    }
                    else
                    {
                        sizes[i] = MathF.Max(0f, resolved);
                    }
                    break;

                case GridTrackKind.Auto:
                default:
                    sizes[i] = MeasureTrack(i, placements, isColumnAxis, container, context, originX, startY, colSizesForRowMeasure);
                    break;
            }
            if (float.IsNaN(sizes[i]) || sizes[i] < 0f) sizes[i] = 0f; // T-19-06: clamp NaN/negative
        }

        // Step 3 — distribute remaining free space across fr tracks (definite axis only).
        if (definiteAxis)
        {
            float usedNonFrAndFloors = 0f;
            float sumFr = 0f;
            for (int i = 0; i < n; i++)
            {
                usedNonFrAndFloors += sizes[i]; // fr tracks contribute their floor (0 unless minmax min)
                if (isFr[i]) sumFr += frValue[i];
            }

            float free = available - usedNonFrAndFloors;
            if (free > 0f && sumFr > 0f)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!isFr[i]) continue;
                    float share = free * (frValue[i] / sumFr);
                    float target = sizes[i] + share; // sizes[i] holds the fr floor
                    if (target > frMax[i]) target = frMax[i]; // minmax max clamp
                    sizes[i] = MathF.Max(0f, target);
                }
            }
        }
        else
        {
            // Indefinite axis: fr tracks have no free space to distribute → size to their content (auto).
            for (int i = 0; i < n; i++)
                if (isFr[i])
                    sizes[i] = MeasureTrack(i, placements, isColumnAxis, container, context, originX, startY, colSizesForRowMeasure);
        }

        for (int i = 0; i < n; i++)
            if (float.IsNaN(sizes[i]) || sizes[i] < 0f) sizes[i] = 0f;

        return sizes;
    }

    // Resolve a minmax() track. The max sub-track determines whether the track is flexible (fr max)
    // and the cap; the min sub-track determines the floor. Non-fr max → a fixed-clamped track.
    private void ResolveMinMax(
        GridTrack t, float axisSize, bool definiteAxis, int trackIndex,
        List<GridPlacement> placements, bool isColumnAxis,
        GridContainerBox container, LayoutContext context, float originX, float startY, float[]? colSizesForRowMeasure,
        out float resolved, out bool isFr, out float frValue, out float minFloor, out float maxCap)
    {
        resolved = 0f; isFr = false; frValue = 0f; minFloor = 0f; maxCap = float.PositiveInfinity;

        float ResolveSub(GridTrack? sub)
        {
            if (sub == null) return 0f;
            return sub.Kind switch
            {
                GridTrackKind.Length => MathF.Max(0f, sub.Length),
                GridTrackKind.Percent => definiteAxis ? MathF.Max(0f, sub.Percent * axisSize) : MeasureTrack(trackIndex, placements, isColumnAxis, container, context, originX, startY, colSizesForRowMeasure),
                _ => MeasureTrack(trackIndex, placements, isColumnAxis, container, context, originX, startY, colSizesForRowMeasure), // auto/content
            };
        }

        minFloor = ResolveSub(t.Min);

        if (t.Max is { Kind: GridTrackKind.Fraction } maxFr)
        {
            // minmax(min, <fr>): a flexible track whose fr participates in distribution, floored by min.
            isFr = true;
            frValue = MathF.Max(0f, maxFr.Fraction);
            maxCap = float.PositiveInfinity;
            resolved = minFloor;
        }
        else
        {
            // minmax(min, <fixed/%/auto>): clamp the content/auto size into [min, max].
            maxCap = ResolveSub(t.Max);
            float content = MeasureTrack(trackIndex, placements, isColumnAxis, container, context, originX, startY, colSizesForRowMeasure);
            resolved = MathF.Max(minFloor, MathF.Min(content, maxCap));
        }
    }

    // Max-content (column) / max content-height (row) of all items whose span covers `trackIndex`.
    private float MeasureTrack(
        int trackIndex, List<GridPlacement> placements, bool isColumnAxis,
        GridContainerBox container, LayoutContext context, float originX, float startY, float[]? colSizesForRowMeasure)
    {
        float max = 0f;
        foreach (var p in placements)
        {
            bool covers = isColumnAxis
                ? trackIndex >= p.ColStart && trackIndex < p.ColStart + p.ColSpan
                : trackIndex >= p.RowStart && trackIndex < p.RowStart + p.RowSpan;
            if (!covers) continue;

            // Only single-track spanners contribute to a track's intrinsic size in the first cut
            // (avoids distributing a multi-track item's size — a Plan-04 refinement).
            int span = isColumnAxis ? p.ColSpan : p.RowSpan;
            if (span > 1) continue;

            float measureWidth = isColumnAxis
                ? (container.Width > 0f ? container.Width : context.AvailableWidth)
                : (colSizesForRowMeasure != null && p.ColStart < colSizesForRowMeasure.Length ? colSizesForRowMeasure[p.ColStart] : context.AvailableWidth);
            if (measureWidth <= 0f) measureWidth = context.AvailableWidth > 0f ? context.AvailableWidth : 1000f;

            var (w, h) = MeasureContentMain(p.Box, measureWidth, context, originX, startY);
            max = MathF.Max(max, isColumnAxis ? w : h);
        }
        return max;
    }

    // Lay the item out into a throwaway output list to obtain its intrinsic content size.
    // Mirrors FlexLayoutEngine.MeasureContent: max-content width = max emitted right-edge − origin;
    // content height = the Layout return value. Save/restore WidthRaw (T-19-07).
    private (float Width, float Height) MeasureContentMain(BoxNode box, float measureWidth,
        LayoutContext context, float originX, float startY)
    {
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
        var savedWidthRaw = box.WidthRaw;
        float measuredHeight = _blockEngine.Layout(box, measureCtx, measureOut, 0);
        box.WidthRaw = savedWidthRaw;

        float maxRight = originX;
        foreach (var pe in measureOut)
            maxRight = MathF.Max(maxRight, pe.Position.X + pe.Position.Width);
        float intrinsicWidth = MathF.Max(0f, maxRight - originX);

        // An item with a definite outer width (e.g. width:100px) contributes that width to a
        // content-sized (auto) track even when it emits no in-flow children of its own — otherwise an
        // empty fixed-width box collapses the auto track to 0. Max-content of a definite-width box is
        // its specified width. (Caught by GridLayoutTests.AutoPlacementColumn_WrapsToNextColumn: an
        // auto implicit column held a definite-width item but resolved to 0 px.)
        if (box.Width > 0f)
            intrinsicWidth = MathF.Max(intrinsicWidth, box.Width);

        return (intrinsicWidth, MathF.Max(0f, measuredHeight));
    }

    // ---- POSITIONING -------------------------------------------------------------------------

    // Cumulative track-start offsets (offset[i] = Σ sizes[0..i-1] + i gaps).
    private static float[] CumulativeOffsets(float[] sizes, float gap)
    {
        var offsets = new float[sizes.Length];
        float acc = 0f;
        for (int i = 0; i < sizes.Length; i++)
        {
            offsets[i] = acc;
            acc += sizes[i] + gap;
        }
        return offsets;
    }

    // Total extent of a track group including interior gaps (no trailing gap).
    private static float TracksExtent(float[] sizes, float gap)
    {
        if (sizes.Length == 0) return 0f;
        float sum = 0f;
        foreach (var s in sizes) sum += s;
        return sum + gap * (sizes.Length - 1);
    }

    // Size spanned by [start, start+span) tracks including interior gaps.
    private static float SpanSize(float[] sizes, float gap, int start, int span)
    {
        float total = 0f;
        int end = Math.Min(sizes.Length, start + span);
        for (int i = start; i < end; i++)
        {
            total += sizes[i];
            if (i < end - 1) total += gap;
        }
        return MathF.Max(0f, total);
    }

    // justify-content / align-content: leading offset of the whole track group within the container.
    // Only the leading offset is applied (space-* approximated by leading for the first cut, since
    // inter-track distribution would change cell offsets — deferred to Plan 04). start = 0.
    private static float ApplyContentAlignment(string alignment, float containerSize, float tracksExtent)
    {
        float free = containerSize - tracksExtent;
        if (free <= 0f) return 0f;
        return alignment switch
        {
            "end" => free,
            "center" => free / 2f,
            "space-around" => free / 2f,   // first-cut: center the group (D-01 approximation)
            "space-evenly" => free / 2f,
            "space-between" => 0f,
            "stretch" => 0f,
            _ => 0f,                        // start / normal
        };
    }

    // Translate the item into its cell, apply justify-self/align-self within the cell, recurse via
    // the existing dispatch. Mirrors FlexLayoutEngine.EmitItem (save/restore Width/WidthRaw/Height,
    // T-19-07).
    private void EmitItem(GridPlacement p, GridContainerBox container,
        float cellX, float cellY, float cellW, float cellH,
        LayoutContext context, List<PositionedElement> output, int pageIndex)
    {
        var box = p.Box;

        string justify = box.JustifySelf is { } js && !string.Equals(js, "auto", StringComparison.OrdinalIgnoreCase)
            ? js : container.JustifyItems;
        string align = box.AlignSelf is { } al && !string.Equals(al, "auto", StringComparison.OrdinalIgnoreCase)
            ? al : container.AlignItems;

        // Item size within the cell. stretch fills the cell; otherwise use the explicit/measured size.
        float itemWidth, itemHeight;
        if (string.Equals(justify, "stretch", StringComparison.OrdinalIgnoreCase) || box.Width <= 0f)
        {
            itemWidth = cellW;
        }
        else
        {
            itemWidth = MathF.Min(box.Width, cellW);
        }
        if (string.Equals(align, "stretch", StringComparison.OrdinalIgnoreCase) || box.Height <= 0f)
        {
            itemHeight = cellH;
        }
        else
        {
            itemHeight = MathF.Min(box.Height, cellH);
        }

        // Position within the cell (start/end/center; stretch already filled).
        float itemX = cellX + AxisOffset(justify, cellW, itemWidth);
        float itemY = cellY + AxisOffset(align, cellH, itemHeight); // D-01: baseline ≈ start

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
        // Recurse through the EXISTING dispatch so nested grid/flex/block/inline/table compose.
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

    // start/end/center offset of an item within its cell along one axis. stretch fills (offset 0).
    private static float AxisOffset(string alignment, float cellSize, float itemSize)
    {
        float room = MathF.Max(0f, cellSize - itemSize);
        return alignment switch
        {
            "end" => room,
            "center" => room / 2f,
            "stretch" => 0f,
            "baseline" => 0f, // D-01: baseline alignment approximated as start
            _ => 0f,          // start / normal
        };
    }
}
