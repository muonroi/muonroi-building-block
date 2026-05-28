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
                break;
        }

        return box;
    }

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

        var display = (node.Style.GetValue("display") ?? "block").Trim().ToLowerInvariant();
        return display switch
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

            var fontWeight = style.GetValue("font-weight");
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
        foreach (var child in node.Children)
        {
            var boxNode = BuildNode(child);
            if (boxNode != null)
                result.Add(boxNode);
        }
        return result;
    }

    // CSS 2.1 §9.2.1: wrap inline siblings in AnonymousBox when block-level siblings are present
    private static List<BoxNode> NormalizeChildren(List<BoxNode> children)
    {
        bool hasBlockLevel = false;
        foreach (var child in children)
        {
            if (child is BlockBox or AnonymousBox or TableBox)
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
            if (child is BlockBox or AnonymousBox or TableBox)
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
