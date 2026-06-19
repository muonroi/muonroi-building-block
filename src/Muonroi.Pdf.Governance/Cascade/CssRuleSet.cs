using AngleSharp.Css.Dom;

namespace Muonroi.Pdf.Governance.Cascade;

/// <summary>
/// A CSS declaration extracted from a style rule: property name (lowercased), value, and the
/// !important flag.
/// </summary>
internal sealed record CssDeclaration(string Property, string Value, bool Important);

/// <summary>
/// A single CSS rule entry ready for cascade matching: one simple selector, its specificity (packed
/// as ids*10000 + classes*100 + tags), its position in document order, and the declarations.
/// </summary>
internal sealed record CssMatchableRule(
    string SelectorText,
    int Specificity,
    int SourceOrder,
    IReadOnlyList<CssDeclaration> Declarations);

/// <summary>
/// Document-level CSS rule index, built once per document from <c>document.StyleSheets</c>.
/// Grouped selectors (e.g. <c>.a, .b { }</c>) are split into individual <see cref="CssMatchableRule"/>
/// entries so each carries its own specificity and source order. Only <see cref="ICssStyleRule"/>
/// entries are collected; @page / @font-face / @import / @keyframes rules are ignored.
/// </summary>
internal sealed class CssRuleSet
{
    private CssRuleSet(IReadOnlyList<CssMatchableRule> rules) => Rules = rules;

    /// <summary>All matchable rules in document order (split, one per simple selector).</summary>
    public IReadOnlyList<CssMatchableRule> Rules { get; }

    /// <summary>
    /// Builds a <see cref="CssRuleSet"/> by walking every author style sheet in
    /// <paramref name="document"/>. No calls to <c>GetComputedStyle</c> or
    /// <c>element.Matches</c> are made here — this is a pure collection step.
    /// </summary>
    public static CssRuleSet FromDocument(IDocument document)
    {
        var result = new List<CssMatchableRule>();
        int sourceOrder = 0;

        foreach (ICssStyleSheet sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            ICssRuleList rules = sheet.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] is not ICssStyleRule styleRule)
                    continue;   // skip @page, @font-face, @import, @keyframes, etc.

                // Collect declarations from the rule's style block.
                IReadOnlyList<CssDeclaration> declarations = CollectDeclarations(styleRule.Style);

                // Split the selector on top-level commas to produce one entry per simple selector.
                string[] simpleSelectors = SplitGroupedSelector(styleRule.SelectorText);

                foreach (string simpleSelector in simpleSelectors)
                {
                    // Specificity strategy:
                    // • If the rule has a single (non-grouped) selector, ICssStyleRule.Selector is
                    //   a single ISelector whose Specificity (AngleSharp.Css.Priority) is exact.
                    //   We read it and pack as ids*10000 + classes*100 + tags.
                    // • For grouped rules (split selectors), the ISelector is a list selector and
                    //   does not have per-simple-selector specificity; fall back to manual CSS 2.1
                    //   §6.4.3 computation via the split string.
                    int specificity = simpleSelectors.Length == 1
                        ? ComputeSpecificityFromSelector(styleRule.Selector)
                        : ComputeSpecificityFromText(simpleSelector);

                    result.Add(new CssMatchableRule(
                        SelectorText: simpleSelector.Trim(),
                        Specificity: specificity,
                        SourceOrder: sourceOrder++,
                        Declarations: declarations));
                }
            }
        }

        return new CssRuleSet(result);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IReadOnlyList<CssDeclaration> CollectDeclarations(ICssStyleDeclaration? style)
    {
        if (style is null || style.Length == 0)
            return Array.Empty<CssDeclaration>();

        var list = new List<CssDeclaration>(style.Length);
        for (int pi = 0; pi < style.Length; pi++)
        {
            string property = style[pi].ToLowerInvariant();
            string value = style.GetPropertyValue(property) ?? string.Empty;
            if (string.IsNullOrEmpty(value))
                continue;   // skip empty values (can happen for shorthand placeholders)

            bool important = style.GetPropertyPriority(property) == "important";
            list.Add(new CssDeclaration(property, value, important));
        }
        return list;
    }

    /// <summary>
    /// Reads specificity from a parsed <see cref="ISelector"/> (the reliable path for
    /// single-selector rules). Returns ids*10000 + classes*100 + tags.
    /// </summary>
    private static int ComputeSpecificityFromSelector(ISelector? selector)
    {
        if (selector is null)
            return 0;

        AngleSharp.Css.Priority p = selector.Specificity;
        return p.Ids * 10000 + p.Classes * 100 + p.Tags;
    }

    /// <summary>
    /// Manual CSS 2.1 §6.4.3 specificity for a split simple-selector string. Used when the
    /// ICssStyleRule was a grouped selector and ISelector.Specificity is for the list, not the part.
    /// a = count of #id tokens; b = count of .class / [attr] / :pseudo-class; c = tag / ::pseudo-element.
    /// </summary>
    private static int ComputeSpecificityFromText(string selectorText)
    {
        string s = selectorText.Trim();
        if (s == "*" || string.IsNullOrEmpty(s))
            return 0;

        int ids = 0, classes = 0, tags = 0;
        bool inBracket = false;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (c == '[') { inBracket = true; classes++; continue; }
            if (c == ']') { inBracket = false; continue; }
            if (inBracket) continue;

            if (c == '#')
            {
                ids++;
                // advance past the id name
                i++;
                while (i < s.Length && IsIdentChar(s[i])) i++;
                i--;
                continue;
            }

            if (c == '.')
            {
                classes++;
                i++;
                while (i < s.Length && IsIdentChar(s[i])) i++;
                i--;
                continue;
            }

            if (c == ':')
            {
                // ::pseudo-element counts as tag; :pseudo-class counts as class
                bool isElement = i + 1 < s.Length && s[i + 1] == ':';
                if (isElement) { tags++; i++; } else { classes++; }
                i++;
                while (i < s.Length && IsIdentChar(s[i])) i++;
                i--;
                continue;
            }

            // tag / element names start with a letter or _ (not *, not a combinator or space)
            if (char.IsLetter(c) || c == '_')
            {
                // combinators (space, >, +, ~) are handled by skipping non-ident chars
                tags++;
                i++;
                while (i < s.Length && IsIdentChar(s[i])) i++;
                i--;
                continue;
            }

            // combinator characters (space, >, +, ~) — no contribution; skip
        }

        return ids * 10000 + classes * 100 + tags;
    }

    private static bool IsIdentChar(char c) =>
        char.IsLetterOrDigit(c) || c == '-' || c == '_';

    /// <summary>
    /// Splits a grouped selector string on top-level commas (commas inside brackets are skipped).
    /// E.g. <c>".a, .b"</c> → <c>[".a", ".b"]</c>.
    /// </summary>
    private static string[] SplitGroupedSelector(string selectorText)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < selectorText.Length; i++)
        {
            char c = selectorText[i];
            if (c == '(' || c == '[') { depth++; continue; }
            if (c == ')' || c == ']') { depth--; continue; }
            if (c == ',' && depth == 0)
            {
                string part = selectorText.Substring(start, i - start).Trim();
                if (!string.IsNullOrEmpty(part))
                    parts.Add(part);
                start = i + 1;
            }
        }

        string last = selectorText.Substring(start).Trim();
        if (!string.IsNullOrEmpty(last))
            parts.Add(last);

        return parts.Count > 0 ? parts.ToArray() : [selectorText.Trim()];
    }
}
