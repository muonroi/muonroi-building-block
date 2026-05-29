using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

// CSS 2.1 §17.5.2: column sizing + colspan/rowspan.
// PITFALL 3 (RESEARCH.md): non-spanning cells sized first, then spanning cells in ascending colspan order.
internal sealed class TableLayoutEngine
{
    private readonly BlockLayoutEngine _blockEngine;
    private readonly InlineLayoutEngine _inlineEngine;

    internal TableLayoutEngine(BlockLayoutEngine blockEngine, InlineLayoutEngine inlineEngine)
    {
        _blockEngine = blockEngine;
        _inlineEngine = inlineEngine;
    }

    public float Layout(TableBox table, LayoutContext context, List<PositionedElement> output, int pageIndex)
    {
        float tableWidth = table.Width > 0f ? table.Width : context.AvailableWidth;
        float borderSpacing = table.BorderSpacing;
        if (string.Equals(table.BorderCollapse, "collapse", StringComparison.OrdinalIgnoreCase))
            borderSpacing = 0f;
        float startY = context.CurrentY;
        float tableX = context.PageMarginLeftPt;

        var rows = CollectRows(table);
        if (rows.Count == 0) return 0f;

        int columnCount = ComputeColumnCount(rows);
        if (columnCount == 0) return 0f;

        // Assign column indices before computing widths (width calc needs ColumnIndex for spanning).
        AssignColumnIndices(rows, columnCount);

        float[] colWidths = string.Equals(table.TableLayout, "fixed", StringComparison.OrdinalIgnoreCase)
            ? ComputeFixedColumnWidths(rows, columnCount, tableWidth, borderSpacing)
            : ComputeAutoColumnWidths(rows, columnCount, tableWidth, borderSpacing, context.TextMetrics);

        // Absolute X start of each column.
        float[] colX = new float[columnCount];
        colX[0] = tableX + borderSpacing;
        for (int c = 1; c < columnCount; c++)
            colX[c] = colX[c - 1] + colWidths[c - 1] + borderSpacing;

        // Pass 1: measure row heights from non-spanning cells.
        float[] rowHeights = new float[rows.Count];
        for (int r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r])
            {
                if (cell.Rowspan != 1) continue;
                float cellWidth = CellWidth(cell.ColumnIndex, cell.Colspan, colWidths, borderSpacing);
                rowHeights[r] = MathF.Max(rowHeights[r], MeasureCell(cell, cellWidth, context));
            }
        }

        // Pass 2: distribute excess height from spanning cells across their rows.
        for (int r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r])
            {
                if (cell.Rowspan <= 1) continue;
                float cellWidth = CellWidth(cell.ColumnIndex, cell.Colspan, colWidths, borderSpacing);
                float needed = MeasureCell(cell, cellWidth, context);

                int lastRow = Math.Min(r + cell.Rowspan - 1, rows.Count - 1);
                float spanned = SpannedHeight(rowHeights, r, lastRow, borderSpacing);

                if (needed > spanned)
                {
                    float excess = needed - spanned;
                    int span = lastRow - r + 1;
                    for (int sr = r; sr <= lastRow; sr++)
                        rowHeights[sr] += excess / span;
                }
            }
        }

        // Row Y positions in continuous layout space.
        float[] rowY = new float[rows.Count];
        rowY[0] = startY + borderSpacing;
        for (int r = 1; r < rows.Count; r++)
            rowY[r] = rowY[r - 1] + rowHeights[r - 1] + borderSpacing;

        // Final pass: lay out each cell for real and emit PositionedElements.
        for (int r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r])
            {
                // Defense-in-depth: skip any cell whose ColumnIndex is out of the grid.
                if (cell.ColumnIndex < 0 || cell.ColumnIndex >= colX.Length) continue;

                int lastRow = Math.Min(r + cell.Rowspan - 1, rows.Count - 1);
                float cellHeight = SpannedHeight(rowHeights, r, lastRow, borderSpacing);
                float cellX = colX[cell.ColumnIndex];
                float cellWidth = CellWidth(cell.ColumnIndex, cell.Colspan, colWidths, borderSpacing);
                float cellY = rowY[r];

                float contentHeight = MeasureCell(cell, cellWidth, context);
                float vAlignOffset = cell.VerticalAlign switch
                {
                    "middle" => MathF.Max(0f, (cellHeight - contentHeight) / 2f),
                    "bottom" => MathF.Max(0f, cellHeight - contentHeight - cell.PaddingBottom),
                    _ => 0f  // "top" is default
                };
                // Fix A2: pass absolute cell content-left X as ContentOriginX so that
                // inline/block content inside this cell renders at the cell's column position.
                // cellX = colX[cell.ColumnIndex]; content starts at cellX + PaddingLeft + BorderLeft.
                float cellContentOriginX = cellX + cell.PaddingLeft + cell.BorderLeft;
                var cellCtx = CellContext(context, cellWidth, cellY + cell.PaddingTop + vAlignOffset,
                    cellOriginX: cellContentOriginX, cellHeight: cellHeight);
                var cellOut = new List<PositionedElement>();
                // G23b: prevent double-application of WidthRaw % in the final layout pass.
                var savedWidthRaw = cell.WidthRaw;
                cell.Width = cellWidth;
                cell.WidthRaw = null;
                _blockEngine.Layout(cell, cellCtx, cellOut, pageIndex);
                cell.WidthRaw = savedWidthRaw;  // restore defensively

                output.Add(new PositionedElement
                {
                    Position = new Rect(cellX, cellY, cellWidth, cellHeight),
                    Source = cell,
                    PageIndex = pageIndex
                });
                output.AddRange(cellOut);
            }
        }

        float tableHeight = borderSpacing;
        foreach (float h in rowHeights)
            tableHeight += h + borderSpacing;

        context.CurrentY = startY + tableHeight;
        return tableHeight;
    }

    // Measure cell content height without emitting output.
    // cellOriginX is 0 for measurement passes (X doesn't affect height calculation).
    private float MeasureCell(TableCellBox cell, float cellWidth, LayoutContext ctx)
    {
        var mc = CellContext(ctx, cellWidth, ctx.CurrentY, cellOriginX: 0f);
        // G23b: prevent double-application of WidthRaw % inside BlockLayoutEngine.
        // Column solver has already resolved cell.Width from WidthRaw; clear WidthRaw
        // so ResolveWidth inside Layout does not reapply the % against the column width.
        var savedWidthRaw = cell.WidthRaw;
        cell.Width = cellWidth;
        cell.WidthRaw = null;
        float height = _blockEngine.Layout(cell, mc, new List<PositionedElement>(), 0);
        cell.WidthRaw = savedWidthRaw;  // restore defensively for any subsequent reads
        return height;
    }

    // Fix A2: cellOriginX is the absolute X of the cell's content-left edge (colX + borderLeft + paddingLeft).
    // Passing it as ContentOriginX into the child LayoutContext ensures inline and block content
    // inside the cell uses the cell's column X as the left baseline, not the page left margin.
    // Without this, all cell content renders at PageMarginLeftPt regardless of which column it is in.
    //
    // G9 (phase 8.11a): also set ContainingBlockRect to the cell's rect so that abs-pos
    // descendants inside the cell are anchored to the cell, not to the page. Per CSS 2.1 §10.1
    // table cells always establish a containing block for their abs-pos descendants.
    private static LayoutContext CellContext(LayoutContext parent, float cellWidth, float startY,
        float cellOriginX = 0f, float cellHeight = 0f) => new()
    {
        PageWidth = parent.PageWidth,
        PageHeight = parent.PageHeight,
        AvailableWidth = cellWidth,
        CurrentY = startY,
        CurrentPageIndex = parent.CurrentPageIndex,
        TotalPages = parent.TotalPages,
        TextMetrics = parent.TextMetrics,
        PageMargins = parent.PageMargins,
        ContentOriginX = cellOriginX,  // Fix A2: cell absolute column X
        // G9: cell establishes containing block for abs-pos children (CSS 2.1 §10.1).
        // Only set when we have real dimensions (non-measurement pass where cellOriginX > 0).
        ContainingBlockRect = cellOriginX > 0f && (cellWidth > 0f || cellHeight > 0f)
            ? new Rect(cellOriginX, startY, cellWidth, cellHeight)
            : null
    };

    private static float CellWidth(int colIndex, int colspan, float[] colWidths, float borderSpacing)
    {
        float w = 0f;
        int end = Math.Min(colIndex + colspan, colWidths.Length);
        for (int c = colIndex; c < end; c++)
        {
            w += colWidths[c];
            if (c < end - 1) w += borderSpacing;
        }
        return w;
    }

    private static float SpannedHeight(float[] rowHeights, int from, int to, float borderSpacing)
    {
        float h = 0f;
        for (int r = from; r <= to; r++)
        {
            h += rowHeights[r];
            if (r < to) h += borderSpacing;
        }
        return h;
    }

    // Collect all TableRowBox children from row groups (header → body → footer).
    private static List<List<TableCellBox>> CollectRows(TableBox table)
    {
        var headers = new List<TableRowGroupBox>();
        var bodies = new List<TableRowGroupBox>();
        var footers = new List<TableRowGroupBox>();

        foreach (var child in table.Children)
        {
            if (child is not TableRowGroupBox g) continue;
            switch (g.GroupType)
            {
                case TableRowGroupType.Header: headers.Add(g); break;
                case TableRowGroupType.Footer: footers.Add(g); break;
                default: bodies.Add(g); break;
            }
        }

        var result = new List<List<TableCellBox>>();
        foreach (var group in headers.Concat(bodies).Concat(footers))
        {
            foreach (var child in group.Children)
            {
                if (child is not TableRowBox row) continue;
                var cells = row.Children.OfType<TableCellBox>().ToList();
                if (cells.Count > 0) result.Add(cells);
            }
        }
        return result;
    }

    // Max cells per row accounting for colspan.
    private static int ComputeColumnCount(List<List<TableCellBox>> rows)
    {
        int max = 0;
        foreach (var row in rows)
        {
            int cols = row.Sum(c => c.Colspan);
            if (cols > max) max = cols;
        }
        return max;
    }

    // PITFALL 3: track occupied slots for rowspan cells to find next free column.
    private static void AssignColumnIndices(List<List<TableCellBox>> rows, int columnCount)
    {
        bool[,] occupied = new bool[rows.Count, columnCount];

        for (int r = 0; r < rows.Count; r++)
        {
            int col = 0;
            foreach (var cell in rows[r])
            {
                while (col < columnCount && occupied[r, col])
                    col++;

                // Gap 6: col can equal columnCount after skipping occupied slots or after
                // col += cell.Colspan advances past the grid boundary. Skip remaining cells.
                if (col >= columnCount) break;

                cell.ColumnIndex = col;

                for (int dc = 0; dc < cell.Colspan && col + dc < columnCount; dc++)
                for (int dr = 0; dr < cell.Rowspan && r + dr < rows.Count; dr++)
                    occupied[r + dr, col + dc] = true;

                col += cell.Colspan;
            }
        }
    }

    // Fixed: explicit widths from first row, equal distribution for auto columns.
    private static float[] ComputeFixedColumnWidths(List<List<TableCellBox>> rows, int columnCount,
        float tableWidth, float borderSpacing)
    {
        float[] widths = new float[columnCount];
        float spacingTotal = borderSpacing * (columnCount + 1);
        float available = MathF.Max(0f, tableWidth - spacingTotal);

        if (rows.Count > 0)
        {
            foreach (var cell in rows[0])
            {
                if (cell.ColumnIndex < columnCount && cell.Colspan == 1)
                {
                    if (cell.Width > 0f)
                    {
                        widths[cell.ColumnIndex] = cell.Width;
                    }
                    else if (TryParsePercent(cell.WidthRaw, out float pct))
                    {
                        // G20 fix (fixed-mode): resolve WidthRaw % against table width when
                        // Width=-1f (the % sentinel). CSS 2.1 §17.5.1: in fixed table layout
                        // percentage widths are resolved against the table's width.
                        widths[cell.ColumnIndex] = tableWidth * pct / 100f;
                    }
                }
            }
        }

        int autoCols = 0;
        float assigned = 0f;
        for (int c = 0; c < columnCount; c++)
        {
            if (widths[c] > 0f) assigned += widths[c];
            else autoCols++;
        }

        float autoWidth = autoCols > 0 ? MathF.Max(0f, (available - assigned) / autoCols) : 0f;
        for (int c = 0; c < columnCount; c++)
            if (widths[c] <= 0f) widths[c] = autoWidth;

        return widths;
    }

    // Auto: CSS 2.1 §17.5.2 — preferred/min content widths, distributed proportionally.
    // PITFALL 3: non-spanning first, then spanning in ascending colspan order.
    private static float[] ComputeAutoColumnWidths(List<List<TableCellBox>> rows, int columnCount,
        float tableWidth, float borderSpacing, ITextMetrics metrics)
    {
        float[] minW = new float[columnCount];
        float[] prefW = new float[columnCount];

        // Step A: non-spanning cells.
        foreach (var row in rows)
        foreach (var cell in row)
        {
            if (cell.Colspan != 1) continue;
            int c = cell.ColumnIndex;
            if (c >= columnCount) continue;
            (float mn, float pf) = ContentWidths(cell, metrics);
            // G20 fix (auto-mode): honor cell.WidthRaw % as a preferred-width hint.
            // CSS 2.1 §17.5.2: percentage widths on td/th are treated as percentages of
            // the table width. Use max(content-preferred, declared%) so min-content is
            // still the floor and declared% acts as a preferred-width minimum.
            if (TryParsePercent(cell.WidthRaw, out float pct))
            {
                float declared = tableWidth * pct / 100f;
                pf = MathF.Max(pf, declared);
                mn = MathF.Max(mn, declared);
            }
            if (mn > minW[c]) minW[c] = mn;
            if (pf > prefW[c]) prefW[c] = pf;
        }

        // Step B: spanning cells in ascending colspan order.
        var spanning = rows.SelectMany(r => r)
            .Where(c => c.Colspan > 1)
            .OrderBy(c => c.Colspan);

        foreach (var cell in spanning)
        {
            int s = cell.ColumnIndex;
            int e = Math.Min(s + cell.Colspan - 1, columnCount - 1);
            if (s >= columnCount) continue;

            (float mn, float pf) = ContentWidths(cell, metrics);

            float sumMin = 0f, sumPref = 0f;
            for (int c = s; c <= e; c++) { sumMin += minW[c]; sumPref += prefW[c]; }
            sumMin += borderSpacing * (e - s);
            sumPref += borderSpacing * (e - s);

            if (mn > sumMin)
            {
                float each = (mn - sumMin) / (e - s + 1);
                for (int c = s; c <= e; c++) minW[c] += each;
            }
            if (pf > sumPref)
            {
                float each = (pf - sumPref) / (e - s + 1);
                for (int c = s; c <= e; c++) prefW[c] += each;
            }
        }

        // Step C: distribute available width.
        float spacing = borderSpacing * (columnCount + 1);
        float avail = MathF.Max(0f, tableWidth - spacing);
        float totalMin = minW.Sum();
        float totalPref = prefW.Sum();
        float[] result = new float[columnCount];

        if (totalPref <= avail)
        {
            float bonus = columnCount > 0 ? (avail - totalPref) / columnCount : 0f;
            for (int c = 0; c < columnCount; c++)
                result[c] = prefW[c] + bonus;
        }
        else if (totalMin <= avail)
        {
            float range = totalPref - totalMin;
            float scale = range > 0f ? (avail - totalMin) / range : 1f;
            for (int c = 0; c < columnCount; c++)
                result[c] = minW[c] + (prefW[c] - minW[c]) * scale;
        }
        else
        {
            float scale = totalMin > 0f ? avail / totalMin : 1f;
            for (int c = 0; c < columnCount; c++)
                result[c] = minW[c] * scale;
        }

        return result;
    }

    // Approximate min-content (longest word) and preferred-content (full line) widths.
    private static (float min, float preferred) ContentWidths(TableCellBox cell, ITextMetrics metrics)
    {
        float min = 0f, pref = 0f;
        AccumulateWidths(cell, metrics, ref min, ref pref);
        return (min, pref);
    }

    private static void AccumulateWidths(BoxNode box, ITextMetrics metrics, ref float min, ref float pref)
    {
        if (box is InlineBox inline && !string.IsNullOrEmpty(inline.Text))
        {
            float wordW = 0f, lineW = 0f;
            foreach (char c in inline.Text)
            {
                float cw = metrics.GetCharWidth(c, inline.FontFamily, inline.FontSize, inline.Bold, inline.Italic);
                if (c is ' ' or '\t' or '\n')
                {
                    if (wordW > min) min = wordW;
                    lineW += cw;
                    wordW = 0f;
                }
                else
                {
                    wordW += cw;
                    lineW += cw;
                }
            }
            if (wordW > min) min = wordW;
            if (lineW > pref) pref = lineW;
            return;
        }
        foreach (var child in box.Children)
            AccumulateWidths(child, metrics, ref min, ref pref);
    }

    // G20: parse a CSS percentage string (e.g. "16%", " 16% ") into a float value 0-100.
    // Returns false (no-op) for null, empty, non-numeric prefix, or bare "%".
    private static bool TryParsePercent(string? raw, out float percent)
    {
        percent = 0f;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        ReadOnlySpan<char> span = raw.AsSpan().Trim();
        if (span.IsEmpty || span[^1] != '%') return false;
        span = span[..^1].TrimEnd();
        if (span.IsEmpty) return false;
        if (!float.TryParse(span, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float val)) return false;
        percent = val;
        return true;
    }
}
