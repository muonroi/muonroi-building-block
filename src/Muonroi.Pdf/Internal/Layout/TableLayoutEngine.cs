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
                var cellCtx = CellContext(context, cellWidth, cellY + cell.PaddingTop + vAlignOffset);
                var cellOut = new List<PositionedElement>();
                _blockEngine.Layout(cell, cellCtx, cellOut, pageIndex);

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
    private float MeasureCell(TableCellBox cell, float cellWidth, LayoutContext ctx)
    {
        var mc = CellContext(ctx, cellWidth, ctx.CurrentY);
        return _blockEngine.Layout(cell, mc, new List<PositionedElement>(), 0);
    }

    private static LayoutContext CellContext(LayoutContext parent, float cellWidth, float startY) => new()
    {
        PageWidth = parent.PageWidth,
        PageHeight = parent.PageHeight,
        AvailableWidth = cellWidth,
        CurrentY = startY,
        CurrentPageIndex = parent.CurrentPageIndex,
        TotalPages = parent.TotalPages,
        TextMetrics = parent.TextMetrics,
        PageMargins = parent.PageMargins
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
                if (cell.ColumnIndex < columnCount && cell.Colspan == 1 && cell.Width > 0f)
                    widths[cell.ColumnIndex] = cell.Width;
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
}
