using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class BlockLayoutEngine
{
    private readonly InlineLayoutEngine _inlineEngine = new();

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
    public float Layout(BlockBox box, LayoutContext context, List<PositionedElement> output, int pageIndex, bool isRoot = false)
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
            PageMargins = context.PageMargins
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
                // Case 1: adjacent sibling collapse
                float collapsed = CollapseMargins(prevMarginBottom, childMarginTop);
                childY += collapsed;
                childMarginTop = 0f;
            }

            childContext.CurrentY = childY + childMarginTop;
            float childHeight = DispatchLayout(child, childContext, output, pageIndex);

            prevMarginBottom = child.MarginBottom;
            childY = childContext.CurrentY + childMarginTop + childHeight;
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
            case BlockBox blockChild:
            {
                float h = Layout(blockChild, ctx, output, pageIndex);
                output.Add(new PositionedElement
                {
                    Source = blockChild,
                    Position = new Rect(ctx.PageMarginLeftPt + blockChild.MarginLeft, startY, childWidth, h),
                    PageIndex = pageIndex
                });
                ctx.CurrentY = startY + h + blockChild.MarginBottom;
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
                ctx.CurrentY = startY + h + replacedChild.MarginBottom;
                return h;
            }

            case TableBox tableChild:
            {
                // TODO: Plan 06 fills in TableLayoutEngine
                float h = LayoutTable(tableChild, ctx, output, pageIndex);
                output.Add(new PositionedElement
                {
                    Source = tableChild,
                    Position = new Rect(ctx.PageMarginLeftPt + tableChild.MarginLeft, startY, childWidth, h),
                    PageIndex = pageIndex
                });
                ctx.CurrentY = startY + h + tableChild.MarginBottom;
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

    // Plan 06 replaces this placeholder with the real TableLayoutEngine.
    private static float LayoutTable(TableBox box, LayoutContext ctx, List<PositionedElement> output, int pageIndex)
        => box.Height > 0f ? box.Height : 100f;
}
