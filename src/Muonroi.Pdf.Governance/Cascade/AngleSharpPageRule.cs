namespace Muonroi.Pdf.Governance.Cascade;

internal sealed class AngleSharpPageRule : IPageRule
{
    private AngleSharpPageRule(
        PdfMargins margins,
        string? size,
        string? topLeft,
        string? topCenter,
        string? topRight,
        string? bottomLeft,
        string? bottomCenter,
        string? bottomRight)
    {
        Margins = margins;
        Size = size;
        TopLeftHtml = topLeft;
        TopCenterHtml = topCenter;
        TopRightHtml = topRight;
        BottomLeftHtml = bottomLeft;
        BottomCenterHtml = bottomCenter;
        BottomRightHtml = bottomRight;
    }

    public PdfMargins Margins { get; }
    public string? Size { get; }

    public string? TopLeftHtml { get; }
    public string? TopCenterHtml { get; }
    public string? TopRightHtml { get; }
    public string? BottomLeftHtml { get; }
    public string? BottomCenterHtml { get; }
    public string? BottomRightHtml { get; }

    public bool HasTopMarginBoxes =>
        TopLeftHtml is not null || TopCenterHtml is not null || TopRightHtml is not null;

    public bool HasBottomMarginBoxes =>
        BottomLeftHtml is not null || BottomCenterHtml is not null || BottomRightHtml is not null;

    internal static IPageRule? TryExtract(IDocument document)
    {
        foreach (ICssStyleSheet sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            ICssRuleList rules = sheet.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] is not ICssPageRule pageRule)
                    continue;

                PdfMargins margins = ParsePageMargins(pageRule.Style);
                string? size = NullIfEmpty(pageRule.Style?.GetPropertyValue("size"));

                // AngleSharp.Css does not surface @top-*/@bottom-* margin-box at-rules through
                // ICssPageRule, so parse them from the raw <style> source text instead.
                string rawCss = CollectRawCss(document);
                ExtractMarginBoxes(
                    rawCss,
                    out string? tl, out string? tc, out string? tr,
                    out string? bl, out string? bc, out string? br);

                return new AngleSharpPageRule(margins, size, tl, tc, tr, bl, bc, br);
            }
        }
        return null;
    }

    private static string CollectRawCss(IDocument document)
    {
        var sb = new StringBuilder();
        foreach (IElement style in document.GetElementsByTagName("style"))
            sb.AppendLine(style.TextContent);
        return sb.ToString();
    }

    // --- Margin-box (@top-*/@bottom-*) parsing from raw CSS -----------------------------------

    private static void ExtractMarginBoxes(
        string css,
        out string? topLeft, out string? topCenter, out string? topRight,
        out string? bottomLeft, out string? bottomCenter, out string? bottomRight)
    {
        topLeft = topCenter = topRight = bottomLeft = bottomCenter = bottomRight = null;

        string? pageBlock = FindPageBlock(css);
        if (pageBlock is null)
            return;

        topLeft = ExtractBoxContent(pageBlock, "@top-left");
        topCenter = ExtractBoxContent(pageBlock, "@top-center");
        topRight = ExtractBoxContent(pageBlock, "@top-right");
        bottomLeft = ExtractBoxContent(pageBlock, "@bottom-left");
        bottomCenter = ExtractBoxContent(pageBlock, "@bottom-center");
        bottomRight = ExtractBoxContent(pageBlock, "@bottom-right");
    }

    /// <summary>Returns the inner text of the first <c>@page { ... }</c> block (incl. nested boxes).</summary>
    private static string? FindPageBlock(string css)
    {
        int idx = IndexOfIgnoreCase(css, "@page", 0);
        if (idx < 0)
            return null;

        int brace = css.IndexOf('{', idx);
        if (brace < 0)
            return null;

        int end = MatchBrace(css, brace);
        if (end < 0)
            return null;

        return css.Substring(brace + 1, end - brace - 1);
    }

    /// <summary>
    /// Extracts the rendered <c>content:</c> fragment of the named margin box, or null. The box name
    /// must be followed (after optional whitespace) by <c>{</c> so <c>@top-left</c> never matches
    /// <c>@top-left-corner</c>.
    /// </summary>
    private static string? ExtractBoxContent(string pageBlock, string boxName)
    {
        int from = 0;
        while (true)
        {
            int idx = IndexOfIgnoreCase(pageBlock, boxName, from);
            if (idx < 0)
                return null;

            int p = idx + boxName.Length;
            while (p < pageBlock.Length && char.IsWhiteSpace(pageBlock[p]))
                p++;

            if (p < pageBlock.Length && pageBlock[p] == '{')
            {
                int end = MatchBrace(pageBlock, p);
                if (end < 0)
                    return null;

                string inner = pageBlock.Substring(p + 1, end - p - 1);
                return ExtractContentValue(inner);
            }

            from = idx + boxName.Length;
        }
    }

    private static string? ExtractContentValue(string declarations)
    {
        int idx = IndexOfIgnoreCase(declarations, "content", 0);
        while (idx >= 0)
        {
            int p = idx + "content".Length;
            while (p < declarations.Length && char.IsWhiteSpace(declarations[p]))
                p++;

            if (p < declarations.Length && declarations[p] == ':')
            {
                int semi = declarations.IndexOf(';', p);
                string value = semi < 0
                    ? declarations[(p + 1)..]
                    : declarations.Substring(p + 1, semi - p - 1);
                return ParseContentTokens(value);
            }

            idx = IndexOfIgnoreCase(declarations, "content", idx + "content".Length);
        }
        return null;
    }

    /// <summary>
    /// Flattens a CSS <c>content</c> value (quoted strings + <c>counter(page|pages)</c>) into a
    /// plain-text fragment, leaving the counter tokens verbatim for downstream substitution.
    /// </summary>
    private static string? ParseContentTokens(string value)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < value.Length)
        {
            char c = value[i];
            if (c == '"' || c == '\'')
            {
                char quote = c;
                i++;
                while (i < value.Length && value[i] != quote)
                {
                    if (value[i] == '\\' && i + 1 < value.Length)
                    {
                        sb.Append(value[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        sb.Append(value[i]);
                        i++;
                    }
                }
                i++; // skip closing quote
            }
            else if (MatchAt(value, i, "counter(pages)"))
            {
                sb.Append("counter(pages)");
                i += "counter(pages)".Length;
            }
            else if (MatchAt(value, i, "counter(page)"))
            {
                sb.Append("counter(page)");
                i += "counter(page)".Length;
            }
            else
            {
                i++;
            }
        }

        string result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static int MatchBrace(string s, int open)
    {
        int depth = 0;
        for (int i = open; i < s.Length; i++)
        {
            if (s[i] == '{')
                depth++;
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return -1;
    }

    private static bool MatchAt(string s, int i, string sub) =>
        i + sub.Length <= s.Length
        && string.Compare(s, i, sub, 0, sub.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private static int IndexOfIgnoreCase(string s, string sub, int start) =>
        s.IndexOf(sub, start, StringComparison.OrdinalIgnoreCase);

    // --- Margins / size (unchanged) -----------------------------------------------------------

    private static PdfMargins ParsePageMargins(ICssStyleDeclaration? style)
    {
        if (style is null)
            return PdfMargins.Default10mm;

        string? shorthand = style.GetPropertyValue("margin");
        if (!string.IsNullOrEmpty(shorthand))
            return ParseMarginShorthand(shorthand);

        double top = ParseMm(style.GetPropertyValue("margin-top"), 10);
        double right = ParseMm(style.GetPropertyValue("margin-right"), 10);
        double bottom = ParseMm(style.GetPropertyValue("margin-bottom"), 10);
        double left = ParseMm(style.GetPropertyValue("margin-left"), 10);
        return new PdfMargins(top, right, bottom, left);
    }

    private static PdfMargins ParseMarginShorthand(string shorthand)
    {
        string[] parts = shorthand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => PdfMargins.Uniform(ParseMm(parts[0], 10)),
            2 => new PdfMargins(ParseMm(parts[0], 10), ParseMm(parts[1], 10), ParseMm(parts[0], 10), ParseMm(parts[1], 10)),
            3 => new PdfMargins(ParseMm(parts[0], 10), ParseMm(parts[1], 10), ParseMm(parts[2], 10), ParseMm(parts[1], 10)),
            4 => new PdfMargins(ParseMm(parts[0], 10), ParseMm(parts[1], 10), ParseMm(parts[2], 10), ParseMm(parts[3], 10)),
            _ => PdfMargins.Default10mm
        };
    }

    private static double ParseMm(string? value, double fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        if (value.EndsWith("mm", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value.AsSpan(0, value.Length - 2),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double mm))
            return mm;

        if (value.EndsWith("cm", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value.AsSpan(0, value.Length - 2),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double cm))
            return cm * 10;

        if (value.EndsWith("pt", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value.AsSpan(0, value.Length - 2),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double pt))
            return pt * 0.352778;

        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value.AsSpan(0, value.Length - 2),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double px))
            return px * 0.264583;

        return fallback;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
