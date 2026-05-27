using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class BlockLayoutEngine
{
    internal InlineLayoutEngine InlineEngine { get; } = new();
    private InlineLayoutEngine _inlineEngine => InlineEngine;

    // Set by LayoutEngine after TableLayoutEngine is constructed (avoids circular ctor dependency).
    internal TableLayoutEngine? TableEngine { get; set; }

    // CSS 2.1 §8.3.1: max(positives) + min(negatives) handles mixed-sign margins.
    internal static float CollapseMargins(float a, float b)
    {
        float pos = MathF.Max(MathF.Max(a, b), 0f);
        float neg = MathF.Min(MathF.Min(a, b), 0f);
        return pos + neg;
    }

    // BFC roots prevent margin collapse across their boundary.
    internal static bool IsBfcRoot(BoxNode box)
    {
        if (box is TableCellBox) return true;
        if (box.Display == "inline-block") return true;
        var overflow = box.Source?.Style?.GetValue("overflow");
        if (overflow is "hidden" or "scroll" or "auto") return true;
        // Root element: BlockBox at depth 0 has no parent — caller passes root directly
        return false;
    }

    /// <summary>
    /// Lay out <paramref name="box"/> in a BFC. Returns the height consumed.
    /// Appends <see cref="PositionedElement"/> entries to <paramref name="output"/>.
    /// </summary>
    public float Layout(BoxNode box, LayoutContext context, List<PositionedElement> output, int pageIndex, bool isRoot = false)
    {
        float availableWidth = ResolveWidth(box, context);

        float contentX = context.PageMarginLeftPt + box.MarginLeft + box.BorderLeft + box.PaddingLeft;
        float contentY = context.CurrentY + box.PaddingTop + box.BorderTop;

        // Empty-block collapse (CSS 2.1 §8.3.1 case 3): no children + no border/padding/min-height → height 0
        if (box.Children.Count == 0 && box.PaddingTop + box.PaddingBottom + box.BorderTop + box.BorderBottom == 0f && box.Height <= 0f)
            return 0f;

        float childY = contentY;
        float prevMarginBottom = 0f;
        bool firstChild = true;
        bool bfcRoot = isRoot || IsBfcRoot(box);

        var childContext = new LayoutContext
        {
            PageWidth = context.PageWidth,
            PageHeight = context.PageHeight,
            AvailableWidth = availableWidth - box.PaddingLeft - box.PaddingRight - box.BorderLeft - box.BorderRight,
            CurrentY = childY,
            CurrentPageIndex = context.CurrentPageIndex,
            TotalPages = context.TotalPages,
            TextMetrics = context.TextMetrics,
            PageMargins = context.PageMargins,
            TextAlign = box.TextAlign ?? context.TextAlign  // inherit text-align from container
        };

        foreach (var child in box.Children)
        {
            float childMarginTop = child.MarginTop;

            if (firstChild && !bfcRoot)
            {
                // Case 2: parent-child collapse — parent has no top border/padding separating margins
                if (box.PaddingTop + box.BorderTop == 0f)
                    childMarginTop = 0f; // parent's own MarginTop subsumes this
            }
            else if (!firstChild)
            {
                // Case 1: adjacent sibling collapse — gap = max(prevMb, childMt), not sum
                float collapsed = CollapseMargins(prevMarginBottom, childMarginTop);
                childY += collapsed;
                childMarginTop = 0f;
            }

            childContext.CurrentY = childY + childMarginTop;
            float childHeight = DispatchLayout(child, childContext, output, pageIndex);

            prevMarginBottom = child.MarginBottom;
            // childContext.CurrentY was advanced to (childStart + childHeight) inside DispatchLayout
            childY = childContext.CurrentY;
            firstChild = false;
        }

        // If height is explicit, use it; otherwise use computed content height
        float contentHeight = box.Height > 0f
            ? box.Height
            : childY - contentY + box.PaddingBottom + box.BorderBottom;

        return contentHeight + box.PaddingTop + box.PaddingBottom + box.BorderTop + box.BorderBottom;
    }

    private float DispatchLayout(BoxNode child, LayoutContext ctx, List<PositionedElement> output, int pageIndex)
    {
        float childWidth = ResolveWidth(child, ctx);
        float startY = ctx.CurrentY;

        switch (child)
        {
            case HrBox hr:
            {
                float hrHeight = hr.MarginTopHr + hr.Thickness + hr.MarginBottomHr;
                float hrY = startY + hr.MarginTopHr;
                output.Add(new PositionedElement
                {
                    Source = hr,
                    Position = new Rect(ctx.PageMarginLeftPt, hrY, ctx.AvailableWidth, hr.Thickness),
                    PageIndex = pageIndex
                });
                ctx.CurrentY = startY + hrHeight;
                return hrHeight;
            }

            case BlockBox blockChild:
            {
                // Propagate text-align from this block's context into its child context
                if (blockChild.TextAlign == null && ctx.TextAlign != null)
                    blockChild.TextAlign = ctx.TextAlign;
                float h = Layout(blockChild, ctx, output, pageIndex);
                output.Add(new PositionedElement
                {
                    Source = blockChild,
                    Position = new Rect(ctx.PageMarginLeftPt + blockChild.MarginLeft, startY, childWidth, h),
                    PageIndex = pageIndex
                });
                // Do NOT add MarginBottom here — the parent loop handles margin collapsing separately.
                ctx.CurrentY = startY + h;
                return h;
            }

            case AnonymousBox anonChild:
            case InlineBox:
            {
                var inlines = child is AnonymousBox anon ? anon.Children : new List<BoxNode> { child };
                float h = _inlineEngine.Layout(inlines, ctx, output, pageIndex);
                ctx.CurrentY = startY + h;
                return h;
            }

            case ReplacedBox replacedChild:
            {
                float h = replacedChild.NaturalHeight > 0f ? replacedChild.NaturalHeight : ctx.TextMetrics.GetLineHeight("serif", 12f);
                output.Add(new PositionedElement
                {
                    Source = replacedChild,
                    Position = new Rect(ctx.PageMarginLeftPt + replacedChild.MarginLeft, startY, childWidth, h),
                    PageIndex = pageIndex
                });
                ctx.CurrentY = startY + h;
                return h;
            }

            case TableBox tableChild:
            {
                float h = TableEngine != null
                    ? TableEngine.Layout(tableChild, ctx, output, pageIndex)
                    : (tableChild.Height > 0f ? tableChild.Height : 100f);
                output.Add(new PositionedElement
                {
                    Source = tableChild,
                    Position = new Rect(ctx.PageMarginLeftPt + tableChild.MarginLeft, startY, childWidth, h),
                    PageIndex = pageIndex
                });
                ctx.CurrentY = startY + h;
                return h;
            }

            default:
            {
                ctx.CurrentY = startY;
                return 0f;
            }
        }
    }

    private static float ResolveWidth(BoxNode box, LayoutContext ctx)
    {
        if (box.WidthRaw != null && box.WidthRaw.EndsWith('%') &&
            float.TryParse(box.WidthRaw.AsSpan(0, box.WidthRaw.Length - 1),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float pct))
        {
            return ctx.AvailableWidth * pct / 100f;
        }

        if (box.Width > 0f)
            return box.Width;

        // auto width: available minus horizontal margins/padding/border
        return ctx.AvailableWidth - box.MarginLeft - box.MarginRight
               - box.PaddingLeft - box.PaddingRight
               - box.BorderLeft - box.BorderRight;
    }

}
