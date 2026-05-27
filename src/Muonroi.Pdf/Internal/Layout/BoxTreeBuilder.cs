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

        var displayVal = style.GetValue("display");
        if (displayVal != null)
            box.Display = displayVal.ToLowerInvariant();

        box.PageBreakBefore = style.GetValue("page-break-before");
        box.PageBreakAfter = style.GetValue("page-break-after");
        box.PageBreakInside = style.GetValue("page-break-inside");

        // text-align is an inherited property — live on BoxNode base
        var textAlignVal = style.GetValue("text-align");
        if (textAlignVal != null)
            box.TextAlign = textAlignVal.Trim().ToLowerInvariant();

        if (box is InlineBox inline)
        {
            var fontFamily = style.GetValue("font-family");
            if (fontFamily != null) inline.FontFamily = fontFamily;
            inline.FontSize = fontSize;

            var fontWeight = style.GetValue("font-weight");
            inline.Bold = fontWeight is "bold"
                || (int.TryParse(fontWeight, out int weight) && weight >= 700);

            var fontStyle = style.GetValue("font-style");
            inline.Italic = fontStyle is "italic" or "oblique";

            inline.Color = style.GetValue("color");

            var verticalAlign = style.GetValue("vertical-align");
            if (verticalAlign != null) inline.VerticalAlign = verticalAlign;

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

            // text-decoration
            var textDecoration = style.GetValue("text-decoration");
            if (textDecoration != null)
                inline.TextDecoration = textDecoration.Trim().ToLowerInvariant();
        }
        else if (box is TableBox table)
        {
            var tableLayout = style.GetValue("table-layout");
            if (tableLayout != null) table.TableLayout = tableLayout;

            table.BorderSpacing = ParseLength(style.GetValue("border-spacing"), fontSize);
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

        // Resolve font properties from <li>'s style for the marker
        float fontSize = ParseLength(liNode.Style.GetValue("font-size")) is float fs and > 0f ? fs : 12f;
        string fontFamily = liNode.Style.GetValue("font-family") ?? "serif";
        string? color = liNode.Style.GetValue("color");

        var markerBox = new InlineBox
        {
            Text = markerText,
            FontFamily = fontFamily,
            FontSize = fontSize,
            Color = color,
            Source = liNode
        };

        // Build children, then prepend the marker
        var raw = CollectChildren(liNode);
        var normalized = NormalizeChildren(raw);

        // Prepend marker: insert at the front of the inline flow
        liBox.Children.Add(markerBox);
        liBox.Children.AddRange(normalized);
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
