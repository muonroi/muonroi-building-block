using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class BlockLayoutEngine
{
    internal InlineLayoutEngine InlineEngine { get; } = new();
    private InlineLayoutEngine _inlineEngine => InlineEngine;

    // Set by LayoutEngine after TableLayoutEngine is constructed (avoids circular ctor dependency).
    internal TableLayoutEngine? TableEngine { get; set; }

    // Set by LayoutEngine after FlexLayoutEngine is constructed (same post-construction
    // pattern as TableEngine — breaks the BlockLayoutEngine ↔ FlexLayoutEngine ctor cycle).
    internal FlexLayoutEngine? FlexEngine { get; set; }

    // Set by LayoutEngine after GridLayoutEngine is constructed (same post-construction pattern as
    // FlexEngine/TableEngine — breaks the BlockLayoutEngine ↔ GridLayoutEngine ctor cycle).
    internal GridLayoutEngine? GridEngine { get; set; }

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
        // CSS 2.1 §9.5: floated elements establish a new BFC (TD3).
        if (!string.IsNullOrEmpty(box.FloatValue) && box.FloatValue != "none") return true;
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

        // Detect if this box establishes a containing block for abs-pos children.
        // Primary rule: CSS 2.1 §10.1 — position:relative with explicit dimensions.
        // Pragmatic deviation: overflow:hidden/scroll/auto also establishes a containing block.
        // Rationale: authors use overflow:hidden as a layout boundary (containing floats,
        // isolating content). Abs-pos children inside such a box should be anchored to it,
        // not to the page. CSS 2.1 §10.1 strictly requires position:relative, but
        // overflow:hidden is the dominant authoring convention for this pattern (e.g. HBND_F
        // template: <img position:absolute> inside <div style="overflow:hidden"> inside <td>).
        // Without this deviation the ContainingBlockRect is never set and the image falls back
        // to page top-left coordinates.
        bool isContainingBlock = (box.Position == "relative" && box.Width > 0f && box.Height > 0f)
            || (box.Overflow is "hidden" or "scroll" or "auto" && box.Width > 0f && box.Height > 0f);
        Rect? savedContainingBlock = context.ContainingBlockRect;

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
            TextAlign = box.TextAlign ?? context.TextAlign,  // inherit text-align from container
            ContainingBlockRect = context.ContainingBlockRect,  // propagate from parent by default
            ContentOriginX = context.ContentOriginX  // Fix A2: propagate cell origin into nested blocks
        };

        // If this box is a containing block, set it on the child context now.
        if (isContainingBlock)
            childContext.ContainingBlockRect = new Rect(contentX, contentY, box.Width, box.Height);

        // Deferred abs-pos list for post-normal-flow placement (CSS 2.1 §9.6).
        var deferredAbsPos = new List<(BoxNode Child, Rect ContainingBlock)>();

        // Float accumulator: reset when entering a BFC root; propagate from parent otherwise.
        if (bfcRoot)
        {
            childContext.Exclusions = new List<FloatExclusion>();  // W5: fresh BFC — reset exclusions list
        }
        else
        {
            childContext.Exclusions = context.Exclusions;  // W6: propagate exclusions by same reference within BFC
        }

        // G7b: batch consecutive inline children (InlineBox / LineBreakBox) into a single
        // AnonymousBox so InlineLayoutEngine receives them as one flow, not separate calls.
        // Without batching, each InlineBox child dispatched individually produces its own
        // line — breaking mixed text+element content like <p>Mã lô: <a>X</a></p>.
        var effectiveChildren = BatchInlineChildren(box.Children);

        foreach (var child in effectiveChildren)
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

            // CSS 2.1 §9.5.2: clear handling — advance childY past float bottoms.
            // W7: read from FloatPlacementSolver.ClearY instead of cursor fields.
            if (child.ClearValue is "both" or "left")
                childY = MathF.Max(childY, FloatPlacementSolver.ClearY(FloatSide.Left, childContext.Exclusions));
            if (child.ClearValue is "both" or "right")
                childY = MathF.Max(childY, FloatPlacementSolver.ClearY(FloatSide.Right, childContext.Exclusions));

            childContext.CurrentY = childY + childMarginTop;
            float childHeight = DispatchLayout(child, childContext, output, pageIndex, deferredAbsPos);

            prevMarginBottom = child.MarginBottom;
            // childContext.CurrentY was advanced to (childStart + childHeight) inside DispatchLayout
            childY = childContext.CurrentY;
            firstChild = false;
        }

        // Float container height contribution: container must enclose its floated children.
        // W8: use exclusions list instead of cursor fields.
        float floatBottom = childContext.Exclusions.Count > 0
            ? childContext.Exclusions.Max(e => e.Bottom)
            : childY;
        if (floatBottom > childY) childY = floatBottom;

        // If height is explicit, use it; otherwise use computed content height
        float contentHeight = box.Height > 0f
            ? box.Height
            : childY - contentY + box.PaddingBottom + box.BorderBottom;

        // Post-normal-flow: resolve deferred abs-pos children (CSS 2.1 §9.6).
        foreach (var (absChild, cb) in deferredAbsPos)
        {
            float resolvedLeft = ResolvePositionOffset(absChild.LeftRaw, cb.Width);
            float resolvedRight = ResolvePositionOffset(absChild.RightRaw, cb.Width);
            float resolvedTop = ResolvePositionOffset(absChild.TopRaw, cb.Height);

            float absWidth = absChild.Width > 0f ? absChild.Width : cb.Width;
            float absX = !float.IsNaN(resolvedLeft)
                ? cb.X + resolvedLeft
                : (!float.IsNaN(resolvedRight) ? cb.X + cb.Width - absWidth - resolvedRight : cb.X);
            float absY = !float.IsNaN(resolvedTop) ? cb.Y + resolvedTop : cb.Y;

            float absHeight;
            if (absChild.Height > 0f)
            {
                absHeight = absChild.Height;
            }
            else
            {
                var absCtx = new LayoutContext
                {
                    PageWidth = context.PageWidth,
                    PageHeight = context.PageHeight,
                    AvailableWidth = absWidth,
                    CurrentY = absY,
                    TextMetrics = context.TextMetrics,
                    PageMargins = context.PageMargins
                };
                absHeight = Layout(absChild, absCtx, output, pageIndex);
            }

            output.Add(new PositionedElement
            {
                Source = absChild,
                Position = new Rect(absX, absY, absWidth, absHeight > 0f ? absHeight : absChild.Height),
                PageIndex = pageIndex
            });
        }

        context.ContainingBlockRect = savedContainingBlock;

        return contentHeight + box.PaddingTop + box.PaddingBottom + box.BorderTop + box.BorderBottom;
    }

    private float DispatchLayout(BoxNode child, LayoutContext ctx, List<PositionedElement> output, int pageIndex,
        List<(BoxNode Child, Rect ContainingBlock)>? deferredAbsPos = null)
    {
        float childWidth = ResolveWidth(child, ctx);
        float startY = ctx.CurrentY;

        // CSS 2.1 §9.6: abs-pos handling — deferred to post-normal-flow pass.
        if (child.Position == "absolute")
        {
            var cb = ctx.ContainingBlockRect ?? new Rect(ctx.PageMarginLeftPt, 0f, ctx.AvailableWidth, ctx.PageHeight);
            deferredAbsPos?.Add((child, cb));
            return 0f;
        }

        // CSS 2.1 §9.5: float handling — removed from normal flow, placed at left or right edge.
        if (child.FloatValue != null && child is BlockBox floatBlock)
        {
            float floatWidth = ResolveWidth(floatBlock, ctx);

            // G19/G21 fix: mark the float width as already resolved so the inner Layout call
            // does NOT re-apply WidthRaw (e.g. "30%") against the narrowed measureCtx.AvailableWidth.
            // Without this, "30%" would be resolved twice: once against the page width here
            // (correct, ≈161pt) and once inside Layout against floatWidth (wrong, ≈48pt).
            // Setting Width=floatWidth and clearing WidthRaw=null causes ResolveWidth inside
            // Layout to take the explicit-Width branch instead of the %-branch.
            floatBlock.Width = floatWidth;
            floatBlock.WidthRaw = null;

            float savedY = ctx.CurrentY;
            float floatY = savedY;

            if (child.FloatValue == "left")
            {
                // W9: use FloatPlacementSolver.AvoidCollisions to determine float X (and Y).
                // Step 1: measure float height with a temporary layout pass (height needed by solver).
                float originX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                var measureCtx = new LayoutContext
                {
                    PageWidth        = ctx.PageWidth,
                    PageHeight       = ctx.PageHeight,
                    AvailableWidth   = floatWidth,
                    CurrentY         = savedY,
                    CurrentPageIndex = ctx.CurrentPageIndex,
                    TotalPages       = ctx.TotalPages,
                    TextMetrics      = ctx.TextMetrics,
                    PageMargins      = ctx.PageMargins,
                    ContentOriginX   = originX,
                };
                var measureOutput = new List<PositionedElement>();
                float floatHeight = Layout(floatBlock, measureCtx, measureOutput, pageIndex);

                // Step 2: ask solver for placement — may advance Y if collisions found.
                var cb = new ContainingBlock(originX, ctx.AvailableWidth);
                var (solvedX, solvedY, _) = FloatPlacementSolver.AvoidCollisions(
                    savedY, floatWidth, floatHeight, FloatSide.Left, cb, ctx.Exclusions);
                float floatX = solvedX + floatBlock.MarginLeft;
                floatY = solvedY;

                // Step 3: if solver advanced Y, re-measure at new Y; otherwise use initial measurement.
                if (solvedY > savedY)
                {
                    var remeasureCtx = new LayoutContext
                    {
                        PageWidth        = ctx.PageWidth,
                        PageHeight       = ctx.PageHeight,
                        AvailableWidth   = floatWidth,
                        CurrentY         = solvedY,
                        CurrentPageIndex = ctx.CurrentPageIndex,
                        TotalPages       = ctx.TotalPages,
                        TextMetrics      = ctx.TextMetrics,
                        PageMargins      = ctx.PageMargins,
                        ContentOriginX   = floatX + floatBlock.PaddingLeft + floatBlock.BorderLeft,
                    };
                    floatHeight = Layout(floatBlock, remeasureCtx, output, pageIndex);
                }
                else
                {
                    // Offset measured output to final floatX position
                    float xDelta = floatX - originX;
                    foreach (var pe in measureOutput)
                        output.Add(new PositionedElement
                        {
                            Source = pe.Source,
                            RenderedText = pe.RenderedText,
                            Position = new Rect(pe.Position.X + xDelta, pe.Position.Y, pe.Position.Width, pe.Position.Height),
                            PageIndex = pe.PageIndex
                        });
                }
                ctx.CurrentY = savedY;  // restore — float does not advance normal flow

                output.Add(new PositionedElement
                {
                    Source = floatBlock,
                    Position = new Rect(floatX, floatY, floatWidth, floatHeight),
                    PageIndex = pageIndex
                });
                ctx.Exclusions.Add(new FloatExclusion(floatX, floatY, floatX + floatWidth, floatY + floatHeight, FloatSide.Left));  // W9/W10: mirror into exclusions list
            }
            else // "right"
            {
                // W11: use FloatPlacementSolver.AvoidCollisions to determine right-float X (and Y).
                // Step 1: measure float height with a temporary layout pass.
                float originX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                var measureCtx = new LayoutContext
                {
                    PageWidth        = ctx.PageWidth,
                    PageHeight       = ctx.PageHeight,
                    AvailableWidth   = floatWidth,
                    CurrentY         = savedY,
                    CurrentPageIndex = ctx.CurrentPageIndex,
                    TotalPages       = ctx.TotalPages,
                    TextMetrics      = ctx.TextMetrics,
                    PageMargins      = ctx.PageMargins,
                    ContentOriginX   = originX + ctx.AvailableWidth - floatWidth,
                };
                var measureOutput = new List<PositionedElement>();
                float floatHeight = Layout(floatBlock, measureCtx, measureOutput, pageIndex);

                // Step 2: ask solver for placement.
                var cb = new ContainingBlock(originX, ctx.AvailableWidth);
                var (solvedX, solvedY, _) = FloatPlacementSolver.AvoidCollisions(
                    savedY, floatWidth, floatHeight, FloatSide.Right, cb, ctx.Exclusions);
                float floatX = solvedX - floatBlock.MarginRight;
                floatY = solvedY;

                // Step 3: if solver advanced Y, re-measure at new Y; otherwise use initial measurement.
                if (solvedY > savedY)
                {
                    var remeasureCtx = new LayoutContext
                    {
                        PageWidth        = ctx.PageWidth,
                        PageHeight       = ctx.PageHeight,
                        AvailableWidth   = floatWidth,
                        CurrentY         = solvedY,
                        CurrentPageIndex = ctx.CurrentPageIndex,
                        TotalPages       = ctx.TotalPages,
                        TextMetrics      = ctx.TextMetrics,
                        PageMargins      = ctx.PageMargins,
                        ContentOriginX   = floatX + floatBlock.PaddingLeft + floatBlock.BorderLeft,
                    };
                    floatHeight = Layout(floatBlock, remeasureCtx, output, pageIndex);
                }
                else
                {
                    // Offset measured output to final floatX position
                    float xDelta = floatX - (originX + ctx.AvailableWidth - floatWidth);
                    foreach (var pe in measureOutput)
                        output.Add(new PositionedElement
                        {
                            Source = pe.Source,
                            RenderedText = pe.RenderedText,
                            Position = new Rect(pe.Position.X + xDelta, pe.Position.Y, pe.Position.Width, pe.Position.Height),
                            PageIndex = pe.PageIndex
                        });
                }
                ctx.CurrentY = savedY;  // restore — float does not advance normal flow

                output.Add(new PositionedElement
                {
                    Source = floatBlock,
                    Position = new Rect(floatX, floatY, floatWidth, floatHeight),
                    PageIndex = pageIndex
                });
                ctx.Exclusions.Add(new FloatExclusion(floatX, floatY, floatX + floatWidth, floatY + floatHeight, FloatSide.Right));  // W11/W12: mirror into exclusions list
            }

            // Float is removed from normal flow: do NOT advance ctx.CurrentY.
            return 0f;
        }

        switch (child)
        {
            case HrBox hr:
            {
                float hrHeight = hr.MarginTopHr + hr.Thickness + hr.MarginBottomHr;
                float hrY = startY + hr.MarginTopHr;
                // Fix F2 (phase 8.8): respect ContentOriginX when inside a float child or table cell.
                float hrOriginX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                output.Add(new PositionedElement
                {
                    Source = hr,
                    Position = new Rect(hrOriginX, hrY, ctx.AvailableWidth, hr.Thickness),
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
                // Fix G8 (Phase 8.9): Do not emit a PositionedElement for the body-root box when
                // it has no visual rendering (no background-color, no background-image). The body
                // element's explicit CSS height (e.g. `height:148mm` on HSLA_E) can equal the full
                // page height, making elBottom exceed pageBodyHeight in PaginationEngine and
                // triggering a spurious page break that pushes all content to page 2. The body
                // container is a layout boundary; its children are paginated independently.
                // Guard: if the body root HAS a background-color or background-image, emit normally
                // so the fill/image renders correctly.
                bool suppressBodyBox = blockChild.IsBodyRoot
                    && blockChild.BackgroundColor == null
                    && blockChild.BackgroundImageSrc == null
                    && blockChild.BackgroundGradient == null;

                if (!suppressBodyBox)
                {
                    // CSS 2.1 §9.5: non-floated block children start after any left-float edge.
                    // Fix A2: use ContentOriginX as the left baseline when inside a table cell
                    // (ContentOriginX > 0 means we are inside a cell, not page normal flow).
                    // W13: read startX from FloatPlacementSolver (Exclusions list).
                    float xOrigin = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                    var cbW13 = new ContainingBlock(xOrigin, ctx.AvailableWidth);
                    float blockStartX = FloatPlacementSolver.AvailableWidthAtY(startY, 0f, cbW13, ctx.Exclusions).StartX;
                    float blockX = blockStartX + blockChild.MarginLeft;
                    output.Add(new PositionedElement
                    {
                        Source = blockChild,
                        Position = new Rect(blockX, startY, childWidth, h),
                        PageIndex = pageIndex
                    });
                }

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
                // CSS 2.1 §10.5: explicit CSS height (author stylesheet) takes priority over
                // intrinsic NaturalHeight. NaturalHeight is the fallback for un-styled <img>.
                // Line-height is last resort when the image fails to decode (NaturalHeight=0).
                // G16: G14 fix had this inverted — NaturalHeight (stub 4px) beat CSS height:100px.
                float h = replacedChild.Height > 0f
                    ? replacedChild.Height
                    : replacedChild.NaturalHeight > 0f
                        ? replacedChild.NaturalHeight
                        : ctx.TextMetrics.GetLineHeight("serif", 12f);
                // Fix G2 (phase 8.8): respect ContentOriginX for block-level images inside float
                // children or table cells (same pattern as HrBox Fix F2).
                float imgOriginX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                output.Add(new PositionedElement
                {
                    Source = replacedChild,
                    Position = new Rect(imgOriginX + replacedChild.MarginLeft, startY, childWidth, h),
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
                // Fix G2-table (phase 8.8): respect ContentOriginX for tables inside float children.
                float tableOriginX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                output.Add(new PositionedElement
                {
                    Source = tableChild,
                    Position = new Rect(tableOriginX + tableChild.MarginLeft, startY, childWidth, h),
                    PageIndex = pageIndex
                });
                ctx.CurrentY = startY + h;
                return h;
            }

            case FlexContainerBox flexChild:
            {
                // Mirror the TableBox case: FlexEngine.Layout emits the per-item PositionedElements
                // into `output`; here we emit the CONTAINER element and advance CurrentY.
                float h = FlexEngine != null
                    ? FlexEngine.Layout(flexChild, ctx, output, pageIndex)
                    : (flexChild.Height > 0f ? flexChild.Height : 0f);
                float flexOriginX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                output.Add(new PositionedElement
                {
                    Source = flexChild,
                    Position = new Rect(flexOriginX + flexChild.MarginLeft, startY, childWidth, h),
                    PageIndex = pageIndex
                });
                ctx.CurrentY = startY + h;
                return h;
            }

            case GridContainerBox gridChild:
            {
                // Mirror the FlexContainerBox case: GridEngine.Layout emits the per-item
                // PositionedElements into `output`; here we emit the CONTAINER element and advance CurrentY.
                float h = GridEngine != null
                    ? GridEngine.Layout(gridChild, ctx, output, pageIndex)
                    : (gridChild.Height > 0f ? gridChild.Height : 0f);
                float gridOriginX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
                output.Add(new PositionedElement
                {
                    Source = gridChild,
                    Position = new Rect(gridOriginX + gridChild.MarginLeft, startY, childWidth, h),
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

    /// <summary>
    /// Resolves a raw CSS position offset string to a float value in points.
    /// Returns <see cref="float.NaN"/> when the value is null/empty (not specified).
    /// Percentage values are resolved against <paramref name="containerDimension"/>.
    /// </summary>
    private static float ResolvePositionOffset(string? raw, float containerDimension)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "auto")
            return float.NaN;

        ReadOnlySpan<char> span = raw.AsSpan().Trim();

        if (span.EndsWith("%", StringComparison.Ordinal))
        {
            if (float.TryParse(span[..^1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float pct))
                return pct / 100f * containerDimension;
            return float.NaN;
        }

        if (span.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(span[..^2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float px))
                return px * (float)Units.PxToPt;
            return float.NaN;
        }

        if (span.EndsWith("pt", StringComparison.Ordinal))
        {
            if (float.TryParse(span[..^2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float pt))
                return pt;
            return float.NaN;
        }

        if (span.EndsWith("mm", StringComparison.Ordinal))
        {
            if (float.TryParse(span[..^2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float mm))
                return mm * (float)Units.MmToPt;
            return float.NaN;
        }

        // bare number → px
        if (float.TryParse(span, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float bare))
            return bare * (float)Units.PxToPt;

        return float.NaN;
    }

    /// <summary>
    /// G7b: Groups consecutive inline children (InlineBox / LineBreakBox) that are NOT
    /// already inside an AnonymousBox into a single AnonymousBox so they flow on the same
    /// line through one InlineLayoutEngine call.  Non-inline children (BlockBox, TableBox,
    /// etc.) are passed through unchanged.  If all children are already separated by block-
    /// level siblings the list is returned as-is (no unnecessary wrapping).
    /// </summary>
    private static IReadOnlyList<BoxNode> BatchInlineChildren(IReadOnlyList<BoxNode> children)
    {
        // Fast path: 0 or 1 child — nothing to batch.
        if (children.Count <= 1)
            return children;

        // Scan for consecutive inline runs of length >= 2.
        bool needsBatching = false;
        int runLength = 0;
        foreach (var c in children)
        {
            if (c is InlineBox or LineBreakBox)
            {
                runLength++;
                if (runLength >= 2) { needsBatching = true; break; }
            }
            else
            {
                runLength = 0;
            }
        }

        if (!needsBatching)
            return children;

        var result = new List<BoxNode>(children.Count);
        var pendingInline = new List<BoxNode>();

        foreach (var c in children)
        {
            if (c is InlineBox or LineBreakBox)
            {
                pendingInline.Add(c);
            }
            else
            {
                if (pendingInline.Count == 1)
                {
                    result.Add(pendingInline[0]);
                }
                else if (pendingInline.Count > 1)
                {
                    var anon = new AnonymousBox();
                    anon.Children.AddRange(pendingInline);
                    result.Add(anon);
                }
                pendingInline.Clear();
                result.Add(c);
            }
        }

        if (pendingInline.Count == 1)
        {
            result.Add(pendingInline[0]);
        }
        else if (pendingInline.Count > 1)
        {
            var anon = new AnonymousBox();
            anon.Children.AddRange(pendingInline);
            result.Add(anon);
        }

        return result;
    }

    private static float ResolveWidth(BoxNode box, LayoutContext ctx)
    {
        float width;

        if (box.WidthRaw != null && box.WidthRaw.EndsWith('%') &&
            float.TryParse(box.WidthRaw.AsSpan(0, box.WidthRaw.Length - 1),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float pct))
        {
            width = ctx.AvailableWidth * pct / 100f;
        }
        else if (box.Width > 0f)
        {
            // Fix C2: clamp body element explicit width to available page content area.
            // A <body style="width:210mm"> on an A5-landscape page has CSS width = full
            // page width (595pt) but ctx.AvailableWidth = page - margins (≈538pt).  Per
            // CSS 2.1 §10.3.3 explicit widths are normally honoured for arbitrary blocks,
            // but we conservatively clamp the root body to prevent content from rendering
            // outside the page boundaries.  Non-body blocks with explicit fixed widths
            // are left unchanged (overflow is intentional, e.g. fixed-width banners).
            width = (box.IsBodyRoot && box.Width > ctx.AvailableWidth)
                ? ctx.AvailableWidth
                : box.Width;
        }
        else if (box is ReplacedBox { NaturalWidth: > 0f } replaced)
        {
            // G24: <img> with no CSS width — use intrinsic pixel→pt size instead of
            // stretching to the full container width (CSS 2.1 §10.3.2 replaced elements).
            // NaturalWidth is seeded by BoxTreeBuilder from DecodedImage.Width * Units.PxToPt.
            // max-width/min-width clamps below still apply (e.g. max-width:200pt constrains it).
            width = replaced.NaturalWidth;
        }
        else
        {
            // auto width: available minus horizontal margins/padding/border
            width = ctx.AvailableWidth - box.MarginLeft - box.MarginRight
                    - box.PaddingLeft - box.PaddingRight
                    - box.BorderLeft - box.BorderRight;
        }

        // CSS 2.1 §10.4: apply max-width / min-width clamps when explicitly set.
        if (box.MaxWidth >= 0f && width > box.MaxWidth)
            width = box.MaxWidth;
        if (box.MinWidth >= 0f && width < box.MinWidth)
            width = box.MinWidth;

        return width;
    }

}
