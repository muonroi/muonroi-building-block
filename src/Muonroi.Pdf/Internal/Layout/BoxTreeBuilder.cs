using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class BoxTreeBuilder
{
    private IReadOnlyDictionary<string, DecodedImage>? _resolvedImages;

    /// <summary>Converts an IStyledNode tree into a BlockBox root. Pitfall 6: display:none check happens first in BuildNode.</summary>
    public BlockBox Build(IStyledNode root, IReadOnlyDictionary<string, DecodedImage>? resolvedImages = null)
    {
        _resolvedImages = resolvedImages;
        var box = new BlockBox { Source = root };
        ResolveCssProperties(root.Style, box);
        // Mark body element so ResolveWidth can clamp its explicit width to available area (Fix C2).
        if (string.Equals(root.LocalName, "body", StringComparison.OrdinalIgnoreCase))
            box.IsBodyRoot = true;
        BuildChildren(root, box);
        return box;
    }

    // Ordinal counters per <ol> ancestor node (keyed by IStyledNode reference), scoped to the build call.
    private readonly Dictionary<IStyledNode, int> _olOrdinalCounters = new(ReferenceEqualityComparer.Instance);

    // Stack of list ancestor nodes so <li> can find its nearest <ul>/<ol> parent.
    private readonly Stack<IStyledNode> _listAncestorStack = new();

    private BoxNode? BuildNode(IStyledNode node)
    {
        // Pitfall 6: check display:none FIRST before any other processing
        if (!node.IsText)
        {
            var displayVal = node.Style.GetValue("display");
            if (string.Equals(displayVal, "none", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        if (node.IsText)
        {
            var textBox = new InlineBox { Source = node, Text = node.TextContent };
            return textBox;
        }

        BoxNode box = CreateBox(node);
        ResolveCssProperties(node.Style, box);

        // Mark body element (when built as a child of <html>) so ResolveWidth can clamp
        // its explicit width and G8 suppression in BlockLayoutEngine can skip emitting
        // the body PositionedElement when it has no visual content.
        if (string.Equals(node.LocalName, "body", StringComparison.OrdinalIgnoreCase))
            box.IsBodyRoot = true;

        // Recurse into block containers and table structure — inline boxes are atomic
        switch (box)
        {
            case BlockBox blockBox:
                // List item handling: prepend a synthetic marker inline box
                if (string.Equals(node.LocalName, "li", StringComparison.OrdinalIgnoreCase))
                {
                    BuildChildrenWithListMarker(node, blockBox);
                }
                else
                {
                    BuildChildren(node, blockBox);
                }
                // G18: propagate inherited text properties (Bold, TextTransform) from this block
                // parent down to inline descendants that have not had an explicit author override.
                // CSS 2.1 §6.2: font-weight and text-transform are inherited properties; a block
                // heading like <h2> must pass Bold=true to its text-node InlineBox children.
                if (box.Bold || box.TextTransform != null || box.WordBreak != null || box.WhiteSpace != null)
                    PropagateInheritedTextProps(box, box.Bold, box.TextTransform, box.WordBreak, box.WhiteSpace);
                break;
            case TableBox tableBox:
                BuildChildren(node, tableBox);
                break;
            case TableRowGroupBox rowGroupBox:
                BuildChildren(node, rowGroupBox);
                break;
            case TableRowBox rowBox:
                BuildChildren(node, rowBox);
                break;
            case TableCellBox cellBox:
                BuildChildren(node, cellBox);
                // G18 + Phase 12.4b + G29: propagate Bold/TextTransform/WordBreak/WhiteSpace into cell's inline children.
                if (box.Bold || box.TextTransform != null || box.WordBreak != null || box.WhiteSpace != null)
                    PropagateInheritedTextProps(box, box.Bold, box.TextTransform, box.WordBreak, box.WhiteSpace);
                break;
        }

        return box;
    }

    // HTML5 §15.3 UA stylesheet: elements whose computed display is "inline" by default.
    // AngleSharp in headless mode returns "" (empty string) for display when the value comes
    // from the UA stylesheet (no explicit declaration). The switch default would map "" → BlockBox,
    // breaking inline label-value flow. This table provides the correct UA default.
    // Note: <input>, <button>, <select>, <textarea>, <img> are inline-replaced elements and are
    // handled elsewhere — do NOT add them here.
    private static readonly HashSet<string> UaInlineTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "span", "label", "a", "strong", "em", "b", "i", "u",
        "code", "kbd", "mark", "small", "sub", "sup", "time",
        "cite", "abbr", "q", "var", "samp", "dfn",
        "tt", "s", "del", "ins", "bdo", "bdi", "ruby", "rt",
    };

    private static BoxNode CreateBox(IStyledNode node)
    {
        if (string.Equals(node.LocalName, "img", StringComparison.OrdinalIgnoreCase))
            return new ReplacedBox { Source = node, Src = node.GetAttribute("src") };

        // HTML5 semantic element dispatch — before display-based dispatch
        string localName = node.LocalName?.ToLowerInvariant() ?? string.Empty;

        if (localName == "br")
            return new LineBreakBox { Source = node };

        // <nobr> treated as a single unbreakable inline token (WhiteSpace="nowrap")
        if (localName == "nobr")
            return new InlineBox { Source = node, Text = node.TextContent, WhiteSpace = "nowrap" };

        if (localName == "hr")
        {
            var hrBox = new HrBox { Source = node };
            // Read border-top-width for Thickness; fall back to default 1f
            var borderWidth = node.Style.GetValue("border-top-width") ?? node.Style.GetValue("border-width");
            if (!string.IsNullOrEmpty(borderWidth))
            {
                float parsed = ParseLength(borderWidth);
                if (parsed > 0f) hrBox.Thickness = parsed;
            }
            // Read color
            hrBox.Color = node.Style.GetValue("color");
            return hrBox;
        }

        // HTML elements that carry implicit text-decoration (UA stylesheet semantics)
        if (localName == "u")
            return new InlineBox { Source = node, Text = node.TextContent, TextDecoration = "underline" };

        if (localName is "s" or "strike" or "del")
            return new InlineBox { Source = node, Text = node.TextContent, TextDecoration = "line-through" };

        if (localName == "a")
        {
            var aBox = new InlineBox { Source = node, Text = node.TextContent };
            var href = node.GetAttribute("href");
            if (!string.IsNullOrEmpty(href))
            {
                // Scheme filter: only http, https, mailto and relative URLs (no scheme) are allowed
                string scheme = "";
                if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
                    scheme = uri.Scheme;
                // Empty scheme = relative URL = allowed as-is
                bool allowed = scheme is "" or "http" or "https" or "mailto";
                aBox.LinkHref = allowed ? href : null;
            }
            return aBox;
        }

        // G7 fix: AngleSharp returns "" (not null) for display on UA-inline elements in headless
        // mode. The null-coalescing ?? never fires for "". Map null/empty/whitespace to "inline"
        // for known UA-inline tags; otherwise default to "block".
        // G14 fix: AngleSharp's GetComputedStyle throws for table structure elements with % widths
        // when no viewport is configured; the catch in AngleSharpStyledNode returns
        // AngleSharpComputedStyle.Empty, yielding an empty display string. Without this fallback,
        // <tbody>/<tr>/<td> fall through to BlockBox, TableLayoutEngine.CollectRows finds no
        // TableRowGroupBox children, and the table renders with zero height (silent omission).
        // Apply HTML5 UA stylesheet display mapping BEFORE the inline-vs-block fallback. See G14.
        string rawDisplay = node.Style.GetValue("display") ?? "";
        string effectiveDisplay;
        if (string.IsNullOrWhiteSpace(rawDisplay))
        {
            effectiveDisplay = localName switch
            {
                "table"   => "table",
                "tbody"   => "table-row-group",
                "thead"   => "table-header-group",
                "tfoot"   => "table-footer-group",
                "tr"      => "table-row",
                "td"      => "table-cell",
                "th"      => "table-cell",
                "caption" => "table-caption",
                _         => UaInlineTags.Contains(localName) ? "inline" : "block"
            };
        }
        else
        {
            effectiveDisplay = rawDisplay.Trim().ToLowerInvariant();
        }
        return effectiveDisplay switch
        {
            "block" or "list-item" or "flow-root" => new BlockBox { Source = node },
            "inline" or "inline-block" => new InlineBox { Source = node, Text = node.TextContent },
            "table" => new TableBox { Source = node },
            "table-row-group" or "tbody" => new TableRowGroupBox { Source = node, GroupType = TableRowGroupType.Body },
            "table-header-group" or "thead" => new TableRowGroupBox { Source = node, GroupType = TableRowGroupType.Header },
            "table-footer-group" or "tfoot" => new TableRowGroupBox { Source = node, GroupType = TableRowGroupType.Footer },
            "table-row" or "tr" => new TableRowBox { Source = node },
            "table-cell" or "td" or "th" => new TableCellBox { Source = node },
            _ => new BlockBox { Source = node }
        };
    }

    private void ResolveCssProperties(IComputedStyle style, BoxNode box)
    {
        float fontSize = ParseLength(style.GetValue("font-size")) is float fs and > 0f ? fs : 12f;

        box.MarginTop = ParseLength(style.GetValue("margin-top"), fontSize);
        box.MarginRight = ParseLength(style.GetValue("margin-right"), fontSize);
        box.MarginBottom = ParseLength(style.GetValue("margin-bottom"), fontSize);
        box.MarginLeft = ParseLength(style.GetValue("margin-left"), fontSize);

        // Fix C1: IE-era HTML body legacy margin attributes (leftmargin, topmargin, rightmargin,
        // bottommargin). These are NOT CSS properties — AngleSharp never puts them in computed style.
        // Apply them as px margins only when the CSS cascade has not already set a non-zero value.
        // Ref: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/body
        // (IE-era attributes deprecated but still used in legacy print-HTML templates, e.g. HSLA_E).
        if (string.Equals(box.Source?.LocalName, "body", StringComparison.OrdinalIgnoreCase))
        {
            if (box.MarginLeft == 0f)
            {
                var lm = box.Source?.GetAttribute("leftmargin");
                if (!string.IsNullOrWhiteSpace(lm))
                    box.MarginLeft = ParseLength(lm + "px", fontSize);
            }
            if (box.MarginTop == 0f)
            {
                var tm = box.Source?.GetAttribute("topmargin");
                if (!string.IsNullOrWhiteSpace(tm))
                    box.MarginTop = ParseLength(tm + "px", fontSize);
            }
            if (box.MarginRight == 0f)
            {
                var rm = box.Source?.GetAttribute("rightmargin");
                if (!string.IsNullOrWhiteSpace(rm))
                    box.MarginRight = ParseLength(rm + "px", fontSize);
            }
            if (box.MarginBottom == 0f)
            {
                var bm = box.Source?.GetAttribute("bottommargin");
                if (!string.IsNullOrWhiteSpace(bm))
                    box.MarginBottom = ParseLength(bm + "px", fontSize);
            }
        }

        box.PaddingTop = ParseLength(style.GetValue("padding-top"), fontSize);
        box.PaddingRight = ParseLength(style.GetValue("padding-right"), fontSize);
        box.PaddingBottom = ParseLength(style.GetValue("padding-bottom"), fontSize);
        box.PaddingLeft = ParseLength(style.GetValue("padding-left"), fontSize);

        box.BorderTop = ParseLength(style.GetValue("border-top-width"), fontSize);
        box.BorderRight = ParseLength(style.GetValue("border-right-width"), fontSize);
        box.BorderBottom = ParseLength(style.GetValue("border-bottom-width"), fontSize);
        box.BorderLeft = ParseLength(style.GetValue("border-left-width"), fontSize);

        var widthVal = style.GetValue("width");
        box.Width = widthVal is null or "auto" ? -1f : ParseLength(widthVal, fontSize);
        box.WidthRaw = widthVal;

        // max-width / min-width: AngleSharp returns "" (not null) for non-cascaded properties,
        // so we must guard with IsNullOrEmpty in addition to "auto"/"none" — otherwise
        // ParseLength("") returns 0f and we'd clamp every box width to 0.
        var maxWidthVal = style.GetValue("max-width");
        box.MaxWidth = string.IsNullOrEmpty(maxWidthVal) || maxWidthVal is "auto" or "none"
            ? -1f
            : ParseLength(maxWidthVal, fontSize);

        var minWidthVal = style.GetValue("min-width");
        box.MinWidth = string.IsNullOrEmpty(minWidthVal) || minWidthVal is "auto" or "none"
            ? -1f
            : ParseLength(minWidthVal, fontSize);

        var heightVal = style.GetValue("height");
        box.Height = heightVal is null or "auto" ? -1f : ParseLength(heightVal, fontSize);

        // AngleSharp's GetPropertyValue returns "" (not null) for non-cascaded properties.
        // Guard with IsNullOrEmpty so an empty computed value never clobbers a default/inherited one.
        var displayVal = style.GetValue("display");
        if (!string.IsNullOrEmpty(displayVal))
            box.Display = displayVal.ToLowerInvariant();

        box.PageBreakBefore = style.GetValue("page-break-before");
        box.PageBreakAfter = style.GetValue("page-break-after");
        box.PageBreakInside = style.GetValue("page-break-inside");

        // text-align is an inherited property — live on BoxNode base
        var textAlignVal = style.GetValue("text-align");
        if (!string.IsNullOrWhiteSpace(textAlignVal))
            box.TextAlign = textAlignVal.Trim().ToLowerInvariant();

        // background-color / background-image
        var bgColor = style.GetValue("background-color");
        if (!string.IsNullOrEmpty(bgColor) && !IsTransparentColor(bgColor))
            box.BackgroundColor = bgColor.Trim();

        var bgImage = style.GetValue("background-image");
        if (!string.IsNullOrEmpty(bgImage) && bgImage.Contains("data:", StringComparison.OrdinalIgnoreCase)
            && bgImage.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            int start = bgImage.IndexOf("url(", StringComparison.OrdinalIgnoreCase) + 4;
            int end = bgImage.LastIndexOf(')');
            if (start > 4 && end > start)
            {
                string uri = bgImage[start..end].Trim().Trim('\'', '"');
                if (uri.StartsWith("data:", StringComparison.Ordinal))
                    box.BackgroundImageSrc = uri;
            }
        }

        // float / clear (CSS 2.1 §9.5)
        var floatVal = style.GetValue("float");
        if (!string.IsNullOrEmpty(floatVal) && floatVal is "left" or "right")
            box.FloatValue = floatVal;

        var clearVal = style.GetValue("clear");
        if (!string.IsNullOrEmpty(clearVal) && clearVal is "left" or "right" or "both")
            box.ClearValue = clearVal;

        // position (CSS 2.1 §9.6)
        var positionVal = style.GetValue("position");
        if (!string.IsNullOrEmpty(positionVal) && positionVal is "absolute" or "relative")
            box.Position = positionVal;

        if (box.Position == "absolute")
        {
            box.TopRaw = style.GetValue("top");
            box.LeftRaw = style.GetValue("left");
            box.RightRaw = style.GetValue("right");
        }

        // overflow — stored for containing-block establishment (CSS 2.1 §10.1).
        var overflowVal = style.GetValue("overflow");
        if (!string.IsNullOrEmpty(overflowVal) && overflowVal is "hidden" or "scroll" or "auto")
            box.Overflow = overflowVal;

        // G18: font-weight and text-transform are inherited CSS properties and must be resolved
        // for ALL box types (block AND inline), not just InlineBox. A block-level heading
        // like <h2> must carry Bold=true so it can be propagated to its inline text children.
        // Step 1: read from computed style (works for inline-style declarations and author sheets
        // that AngleSharp cascades correctly).
        var fwAll = style.GetValue("font-weight");
        if (!string.IsNullOrEmpty(fwAll))
            box.Bold = fwAll is "bold"
                || (int.TryParse(fwAll, out int fwInt) && fwInt >= 700);

        // Step 2: UA stylesheet defaults — apply when no author-level font-weight was found.
        // h1..h6 are bold by UA spec; <th> is also bold by UA spec (HTML5 §14.3.9).
        // G23d: added "th" alongside h1-h6 so table header cells default to bold.
        if (string.IsNullOrEmpty(fwAll))
        {
            string localNameForUa = box.Source?.LocalName?.ToLowerInvariant() ?? "";
            if (localNameForUa is "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "th")
                box.Bold = true;
        }

        // G23g: UA stylesheet: <th> default text-align is center (HTML5 §14.3.9).
        // Only applied when no author-level text-align was resolved (computed style +
        // class-rule + descendant-class fallbacks all returned null/empty above).
        if (string.IsNullOrWhiteSpace(box.TextAlign))
        {
            string localNameForUaAlign = box.Source?.LocalName?.ToLowerInvariant() ?? "";
            if (localNameForUaAlign == "th")
                box.TextAlign = "center";
        }

        // Step 3: text-transform — read for all boxes; also try class-rule + descendant fallback
        // so that .text-uppercase { text-transform: uppercase } is picked up when GetComputedStyle throws.
        var ttAll = style.GetValue("text-transform");
        if (!string.IsNullOrEmpty(ttAll) && ttAll == "uppercase")
            box.TextTransform = "uppercase";

        if (box is InlineBox inline)
        {
            // AngleSharp's GetComputedStyle returns "" (not null) when font-family is not cascaded.
            // Guard against both null and empty so the box keeps its default "serif" in that case,
            // which the writer recognises. An empty FontFamily causes OwnedPdfWriter to silently
            // skip the element (FontFamily guard at line ~671). See BuildChildrenWithListMarker
            // for the same pattern applied to list markers.
            var fontFamily = NormalizeFontFamily(style.GetValue("font-family"));
            if (!string.IsNullOrWhiteSpace(fontFamily)) inline.FontFamily = fontFamily;
            inline.FontSize = fontSize;

            // G18: inline.Bold was already initialised by the shared code above (which handles
            // the UA bold default for h1-h6 and explicit author declarations). Only override here
            // when the computed style provides a non-empty explicit value for this inline box
            // itself (e.g. <strong> has font-weight:bold in UA; explicit "normal" overrides UA).
            var fontWeight = style.GetValue("font-weight");
            if (!string.IsNullOrEmpty(fontWeight))
                inline.Bold = fontWeight is "bold"
                    || (int.TryParse(fontWeight, out int weight) && weight >= 700);

            var fontStyle = style.GetValue("font-style");
            inline.Italic = fontStyle is "italic" or "oblique";

            inline.Color = style.GetValue("color");

            var verticalAlign = style.GetValue("vertical-align");
            if (!string.IsNullOrWhiteSpace(verticalAlign)) inline.VerticalAlign = verticalAlign;

            // line-height: normal/null → 1.0f; unitless → factor; px → factor relative to fontSize; % → /100
            var lineHeightVal = style.GetValue("line-height");
            if (!string.IsNullOrEmpty(lineHeightVal) && lineHeightVal != "normal")
            {
                ReadOnlySpan<char> lhSpan = lineHeightVal.AsSpan().Trim();
                if (lhSpan.EndsWith("px", StringComparison.Ordinal))
                {
                    float px = TryParseFloat(lhSpan[..^2]);
                    float ptVal = px * (float)Units.PxToPt;
                    if (fontSize > 0f) inline.LineHeightFactor = ptVal / fontSize;
                }
                else if (lhSpan.EndsWith("%", StringComparison.Ordinal))
                {
                    float pct = TryParseFloat(lhSpan[..^1]);
                    inline.LineHeightFactor = pct / 100f;
                }
                else if (float.TryParse(lhSpan, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float factor) && factor > 0f)
                {
                    inline.LineHeightFactor = factor;
                }
            }

            // text-decoration: only overwrite if the CSS cascade provides a non-empty value.
            // An empty string from AngleSharp's GetComputedStyle must not clear a value that was
            // set explicitly by CreateBox (e.g. for <u>, <s>, <del> elements).
            var textDecoration = style.GetValue("text-decoration");
            if (!string.IsNullOrEmpty(textDecoration))
                inline.TextDecoration = textDecoration.Trim().ToLowerInvariant();

            var textTransform = style.GetValue("text-transform");
            if (!string.IsNullOrEmpty(textTransform) && textTransform == "uppercase")
                inline.TextTransform = "uppercase";

            var whiteSpace = style.GetValue("white-space");
            if (!string.IsNullOrEmpty(whiteSpace) && whiteSpace is "pre-wrap" or "pre-line" or "nowrap")
            {
                // Don't overwrite a CreateBox-set "nowrap" (from <nobr>) with a weaker value
                if (inline.WhiteSpace != "nowrap" || whiteSpace == "nowrap")
                    inline.WhiteSpace = whiteSpace;
            }

            // Phase 12.4: parse word-break / overflow-wrap / word-wrap.
            // All three CSS properties produce character-break behavior on overflow; we
            // normalize to two values: "break-all" (always break) or "break-word" (break only
            // when a token would otherwise overflow the line). word-break has highest precedence.
            string? wb = null;
            var wordBreak = style.GetValue("word-break");
            if (!string.IsNullOrEmpty(wordBreak))
            {
                wb = wordBreak switch
                {
                    "break-all" => "break-all",
                    "break-word" => "break-word",
                    _ => null,
                };
            }
            if (wb is null)
            {
                var overflowWrap = style.GetValue("overflow-wrap");
                if (string.IsNullOrEmpty(overflowWrap))
                    overflowWrap = style.GetValue("word-wrap"); // legacy alias
                if (!string.IsNullOrEmpty(overflowWrap) && overflowWrap is "break-word" or "anywhere")
                    wb = "break-word";
            }
            if (wb is not null)
                inline.WordBreak = wb;
        }
        else if (box is TableBox table)
        {
            var tableLayout = style.GetValue("table-layout");
            if (!string.IsNullOrWhiteSpace(tableLayout)) table.TableLayout = tableLayout;

            table.BorderSpacing = ParseLength(style.GetValue("border-spacing"), fontSize);

            var borderCollapseVal = style.GetValue("border-collapse");
            if (!string.IsNullOrEmpty(borderCollapseVal))
                table.BorderCollapse = borderCollapseVal.Trim().ToLowerInvariant();
        }
        else if (box is TableCellBox cell)
        {
            // colspan/rowspan come from HTML attributes, not computed style
            var colspanAttr = box.Source?.GetAttribute("colspan");
            if (colspanAttr != null && int.TryParse(colspanAttr, out int colspan) && colspan >= 1)
                cell.Colspan = colspan;

            var rowspanAttr = box.Source?.GetAttribute("rowspan");
            if (rowspanAttr != null && int.TryParse(rowspanAttr, out int rowspan) && rowspan >= 1)
                cell.Rowspan = rowspan;

            var vAlign = style.GetValue("vertical-align");
            if (!string.IsNullOrEmpty(vAlign))
                cell.VerticalAlign = vAlign.Trim().ToLowerInvariant();

            // Phase 12.4b: parse word-break / overflow-wrap on the cell with the G23 class-rule
            // fallback. Real templates (e.g. TCIS HBCX) declare it on `.table-bodered2 td` — a
            // class-descendant selector that AngleSharp.Css does not surface through
            // GetComputedStyle. Propagation to inline children happens in BuildNode below.
            cell.WordBreak = ResolveWordBreakWithFallback(cell, style);

            // G29: resolve white-space with the same class/descendant/inline fallback so
            // `white-space: nowrap` declared on `.table-bodered2 td` (or inline on a cell)
            // is honoured on %-width tables; propagated to inline children in BuildNode below.
            cell.WhiteSpace = ResolveWhiteSpaceWithFallback(cell, style);
        }
        else if (box is ReplacedBox replaced && replaced.Src != null && _resolvedImages != null)
        {
            if (_resolvedImages.TryGetValue(replaced.Src, out DecodedImage? decoded))
            {
                replaced.NaturalWidth = decoded.Width * Units.PxToPt;
                replaced.NaturalHeight = decoded.Height * Units.PxToPt;
            }
        }
    }

    /// <summary>
    /// Returns true if the CSS color value represents a fully-transparent color that should
    /// not generate a background-fill rectangle in the PDF content stream.
    ///
    /// AngleSharp normalizes CSS 'transparent' to 'rgba(0, 0, 0, 0)' in computed style.
    /// Without this check, those boxes get BackgroundColor = "rgba(0, 0, 0, 0)" and
    /// ParseColor falls back to black (0,0,0), producing a solid black fill over the page.
    /// </summary>
    private static bool IsTransparentColor(string? color)
    {
        if (string.IsNullOrEmpty(color)) return true;
        string c = color.Trim();
        if (c.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return true;
        if (c.Equals("initial", StringComparison.OrdinalIgnoreCase)) return true;

        // AngleSharp normalises CSS transparent to rgba(0, 0, 0, 0) — catch both
        // the canonical form and any whitespace variants.
        if (c.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            // Extract alpha channel (4th component after the last comma)
            int lastComma = c.LastIndexOf(',');
            if (lastComma >= 0)
            {
                ReadOnlySpan<char> alphaSpan = c.AsSpan(lastComma + 1).Trim().TrimEnd(')').Trim();
                if (float.TryParse(alphaSpan, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float alpha))
                    return alpha == 0f;
            }
        }
        return false;
    }

    // Pitfall 7: percent widths stored as -1f sentinel — resolved during layout, not here
    private static float ParseLength(string? val, float emBase = 12f)
    {
        if (string.IsNullOrWhiteSpace(val) || val == "auto" || val == "normal")
            return 0f;

        if (val.EndsWith('%'))
            return -1f;

        ReadOnlySpan<char> span = val.AsSpan();

        if (span.EndsWith("px", StringComparison.Ordinal))
            return TryParseFloat(span[..^2]) * Units.PxToPt;
        if (span.EndsWith("mm", StringComparison.Ordinal))
            return TryParseFloat(span[..^2]) * Units.MmToPt;
        if (span.EndsWith("cm", StringComparison.Ordinal))
            return TryParseFloat(span[..^2]) * Units.CmToPt;
        if (span.EndsWith("in", StringComparison.Ordinal))
            return TryParseFloat(span[..^2]) * Units.InToPt;
        if (span.EndsWith("pt", StringComparison.Ordinal))
            return TryParseFloat(span[..^2]);
        if (span.EndsWith("em", StringComparison.Ordinal))
            return TryParseFloat(span[..^2]) * emBase * Units.PxToPt;

        if (span.EndsWith("rem", StringComparison.Ordinal))
            return TryParseFloat(span[..^3]) * 16f * (float)Units.PxToPt;

        // Bare number: treat as px
        return TryParseFloat(span) * Units.PxToPt;
    }

    private static float TryParseFloat(ReadOnlySpan<char> span)
    {
        if (float.TryParse(span, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float result))
            return result;
        return 0f;
    }

    // Normalize a CSS font-family value: pick the FIRST family in a comma-separated stack,
    // strip enclosing single/double quotes (CSS keeps them in computed value), and trim.
    // Real templates declare e.g. font-family:"Times New Roman" — without this, the literal
    // quotes leak into BundledFonts.TryGetFallback and the GID map lookup → silent rendering
    // failure (FONT-GID-MAP-MISSING).
    private static string? NormalizeFontFamily(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        ReadOnlySpan<char> s = raw.AsSpan().Trim();
        int comma = s.IndexOf(',');
        if (comma >= 0) s = s[..comma].Trim();
        if (s.Length >= 2 && (s[0] == '"' || s[0] == '\'') && s[^1] == s[0])
            s = s[1..^1].Trim();
        return s.ToString();
    }

    private void BuildChildren(IStyledNode node, BlockBox parent)
    {
        string nodeName = node.LocalName?.ToLowerInvariant() ?? "";
        bool isListContainer = nodeName is "ul" or "ol";
        if (isListContainer)
            _listAncestorStack.Push(node);

        var raw = CollectChildren(node);
        parent.Children.AddRange(NormalizeChildren(raw));

        if (isListContainer)
            _listAncestorStack.Pop();
    }

    private void BuildChildrenWithListMarker(IStyledNode liNode, BlockBox liBox)
    {
        // Set default left padding for list indentation
        float paddingLeft = ParseLength(liNode.Style.GetValue("padding-left"));
        if (paddingLeft <= 0f)
            paddingLeft = 40f * (float)Units.PxToPt; // 40px browser default → pt
        liBox.PaddingLeft = paddingLeft;

        // Determine list type and marker text
        string markerText = DetermineListMarker(liNode);

        // Resolve font properties from <li>'s style for the marker.
        // Use IsNullOrEmpty (not ??) because AngleSharp's GetComputedStyle returns "" (not null)
        // when no font-family is cascaded. An empty family would cause the writer to silently
        // skip the marker PositionedElement (FontFamily guard in BuildContentStream).
        float fontSize = ParseLength(liNode.Style.GetValue("font-size")) is float fs and > 0f ? fs : 12f;
        string? rawFontFamily = NormalizeFontFamily(liNode.Style.GetValue("font-family"));
        string fontFamily = string.IsNullOrWhiteSpace(rawFontFamily) ? "serif" : rawFontFamily;
        string? color = liNode.Style.GetValue("color");

        var markerBox = new InlineBox
        {
            Text = markerText,
            FontFamily = fontFamily,
            FontSize = fontSize,
            Color = color,
            Source = liNode
        };

        // Build children, then combine marker + children into a single inline flow.
        // If we add marker and children as separate BoxNode siblings of the BlockBox, each
        // InlineBox child is dispatched by BlockLayoutEngine as a separate inline-layout call
        // (separate line). Wrapping them all in one AnonymousBox forces InlineLayoutEngine to
        // lay them out together on the same line: "• Item A".
        var raw = CollectChildren(liNode);
        var normalized = NormalizeChildren(raw);

        // Check whether the children contain any block-level boxes (e.g. nested <p>/<div>).
        // If so, put just the marker into an AnonymousBox and keep the block children as-is,
        // so block structure is preserved. For the common case (plain text <li>) all children
        // are inline and should share the marker's row.
        bool hasBlockChild = normalized.Any(n => n is BlockBox or AnonymousBox or TableBox);
        if (hasBlockChild)
        {
            // Marker gets its own anonymous row above the block children — acceptable fallback.
            var markerAnon = new AnonymousBox();
            markerAnon.Children.Add(markerBox);
            liBox.Children.Add(markerAnon);
            liBox.Children.AddRange(normalized);
        }
        else
        {
            // All-inline case: combine marker + inline children into one AnonymousBox so they
            // share a single line in InlineLayoutEngine.
            var inlineRow = new AnonymousBox();
            inlineRow.Children.Add(markerBox);
            inlineRow.Children.AddRange(normalized);
            liBox.Children.Add(inlineRow);
        }
    }

    private string DetermineListMarker(IStyledNode liNode)
    {
        // Use the list ancestor stack (top = nearest list container)
        IStyledNode? listAncestor = _listAncestorStack.Count > 0 ? _listAncestorStack.Peek() : null;
        if (listAncestor == null)
            return "• "; // fallback: unordered

        if (string.Equals(listAncestor.LocalName, "ul", StringComparison.OrdinalIgnoreCase))
            return "• ";

        if (string.Equals(listAncestor.LocalName, "ol", StringComparison.OrdinalIgnoreCase))
        {
            if (!_olOrdinalCounters.TryGetValue(listAncestor, out int counter))
                counter = 0;
            counter++;
            _olOrdinalCounters[listAncestor] = counter;
            return $"{counter}. ";
        }

        return "• ";
    }

    private static IStyledNode? FindListAncestor(IStyledNode node) => null; // not used; stack approach used instead

    private void BuildChildren(IStyledNode node, TableCellBox parent)
    {
        var raw = CollectChildren(node);
        parent.Children.AddRange(NormalizeChildren(raw));
    }

    private void BuildChildren(IStyledNode node, TableBox parent)
    {
        var raw = CollectChildren(node);
        parent.Children.AddRange(raw);
    }

    private void BuildChildren(IStyledNode node, TableRowGroupBox parent)
    {
        var raw = CollectChildren(node);
        parent.Children.AddRange(raw);
    }

    private void BuildChildren(IStyledNode node, TableRowBox parent)
    {
        var raw = CollectChildren(node);
        parent.Children.AddRange(raw);
    }

    private List<BoxNode> CollectChildren(IStyledNode node)
    {
        var result = new List<BoxNode>();
        bool hasElementChild = false;
        foreach (var child in node.Children)
            if (!child.IsText) { hasElementChild = true; break; }

        foreach (var child in node.Children)
        {
            // G7b: drop whitespace-only text nodes when mixed with element children (CSS inter-
            // element whitespace). Keep all non-empty text nodes; whitespace-only text between
            // block siblings is insignificant per CSS spec.
            if (child.IsText && hasElementChild &&
                string.IsNullOrWhiteSpace(child.TextContent))
                continue;

            var boxNode = BuildNode(child);
            if (boxNode != null)
                result.Add(boxNode);
        }
        return result;
    }

    // G18: CSS inheritance for font-weight and text-transform.
    // Walks the subtree rooted at 'node' and copies 'Bold'/'TextTransform' onto each InlineBox
    // descendant that has not had an explicit author override (i.e. its current value is the
    // CSS initial/default: Bold=false, TextTransform=null).
    // This is intentionally narrow — it only propagates these two properties and only in the
    // downward direction (parent → child), which is sufficient for the h1-h6 heading case.
    private static void PropagateInheritedTextProps(BoxNode node, bool parentBold, string? parentTextTransform, string? parentWordBreak = null, string? parentWhiteSpace = null)
    {
        foreach (var child in node.Children)
        {
            if (child is InlineBox inlineChild)
            {
                // Apply parent's Bold only if the child still carries the default (false).
                // A child with Bold=true already had an explicit override (e.g. <strong>).
                if (!inlineChild.Bold && parentBold)
                    inlineChild.Bold = true;
                // Apply parent's TextTransform only if the child has none set.
                if (inlineChild.TextTransform == null && parentTextTransform != null)
                    inlineChild.TextTransform = parentTextTransform;
                // Phase 12.4b: same selective-override pattern for word-break.
                if (inlineChild.WordBreak == null && parentWordBreak != null)
                    inlineChild.WordBreak = parentWordBreak;
                // G29: same selective-override pattern for white-space (don't weaken a
                // <nobr>-set "nowrap" — only fill when the child has none).
                if (inlineChild.WhiteSpace == null && parentWhiteSpace != null)
                    inlineChild.WhiteSpace = parentWhiteSpace;
            }
            else
            {
                // Recurse through anonymous boxes and nested blocks so that deeply nested
                // text-node InlineBoxes also receive the inherited values.
                // Effective inherited value for the child itself (may refine parent's):
                bool childBold = child.Bold || parentBold;
                string? childTextTransform = child.TextTransform ?? parentTextTransform;
                string? childWordBreak = child.WordBreak ?? parentWordBreak;
                string? childWhiteSpace = child.WhiteSpace ?? parentWhiteSpace;
                if (childBold || childTextTransform != null || childWordBreak != null || childWhiteSpace != null)
                    PropagateInheritedTextProps(child, childBold, childTextTransform, childWordBreak, childWhiteSpace);
            }
        }
    }

    /// <summary>
    /// Reads word-break/overflow-wrap/word-wrap from the owned computed style.
    /// Returns normalized "break-all" | "break-word" | null. word-break takes precedence;
    /// overflow-wrap and the legacy word-wrap alias collapse to "break-word" when their value
    /// is "break-word" or "anywhere".
    /// </summary>
    private static string? ResolveWordBreakWithFallback(BoxNode box, IComputedStyle style)
    {
        string? Read(string prop)
        {
            var v = style.GetValue(prop);
            return string.IsNullOrEmpty(v) ? null : v.Trim().ToLowerInvariant();
        }

        var wb = Read("word-break");
        if (wb is "break-all") return "break-all";
        if (wb is "break-word") return "break-word";

        var ow = Read("overflow-wrap") ?? Read("word-wrap");
        if (ow is "break-word" or "anywhere") return "break-word";
        return null;
    }

    // Reads white-space from the owned computed style. Only the values the inline layout engine
    // acts on are returned; anything else collapses to null (default wrapping).
    private static string? ResolveWhiteSpaceWithFallback(BoxNode box, IComputedStyle style)
    {
        var v = style.GetValue("white-space");
        v = v?.Trim().ToLowerInvariant();
        return v is "nowrap" or "pre-wrap" or "pre-line" ? v : null;
    }

    // CSS 2.1 §9.2.1: wrap inline siblings in AnonymousBox when block-level siblings are present.
    // ReplacedBox (e.g. <img>) is block-level for purposes of this normalization — otherwise an
    // <img> sibling to <p> blocks gets wrapped in AnonymousBox, dispatched to InlineLayoutEngine,
    // which cannot render ReplacedBox and emits nothing (silent drop). G26 fix (Phase 12.2).
    private static List<BoxNode> NormalizeChildren(List<BoxNode> children)
    {
        bool hasBlockLevel = false;
        foreach (var child in children)
        {
            if (child is BlockBox or AnonymousBox or TableBox or ReplacedBox)
            {
                hasBlockLevel = true;
                break;
            }
        }

        if (!hasBlockLevel)
            return children;

        var result = new List<BoxNode>(children.Count);
        var pendingInline = new List<BoxNode>();

        foreach (var child in children)
        {
            if (child is BlockBox or AnonymousBox or TableBox or ReplacedBox)
            {
                if (pendingInline.Count > 0)
                {
                    var anonBox = new AnonymousBox();
                    anonBox.Children.AddRange(pendingInline);
                    result.Add(anonBox);
                    pendingInline.Clear();
                }
                result.Add(child);
            }
            else
            {
                pendingInline.Add(child);
            }
        }

        if (pendingInline.Count > 0)
        {
            var anonBox = new AnonymousBox();
            anonBox.Children.AddRange(pendingInline);
            result.Add(anonBox);
        }

        return result;
    }
}
