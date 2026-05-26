using AngleSharp.Css.Dom;

namespace Muonroi.Pdf.Governance.Cascade;

internal sealed class AngleSharpPageRule : IPageRule
{
    private AngleSharpPageRule(PdfMargins margins, string? size)
    {
        Margins = margins;
        Size = size;
    }

    public PdfMargins Margins { get; }
    public string? TopMarginBoxHtml => null;
    public string? BottomMarginBoxHtml => null;
    public string? Size { get; }

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
                return new AngleSharpPageRule(margins, size);
            }
        }
        return null;
    }

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
