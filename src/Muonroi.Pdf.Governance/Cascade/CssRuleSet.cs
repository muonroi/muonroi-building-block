using AngleSharp.Css.Dom;
using AngleSharp.Dom;

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

        // Step A: Build supplemental declaration map from raw <style> text.
        // AngleSharp.Css beta.147 silently drops CSS3 properties (word-break, white-space, etc.)
        // from ICssStyleDeclaration — the CSSOM CssText shows ".t td { }" for rules that authored
        // "word-break: break-word". The only recovery is parsing the raw authored text directly.
        // Key: normalized selector text → supplemental declarations for dropped properties only.
        var supplemental = BuildSupplementalFromRawStyleText(document);

        foreach (ICssStyleSheet sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            ICssRuleList rules = sheet.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] is not ICssStyleRule styleRule)
                    continue;   // skip @page, @font-face, @import, @keyframes, etc.

                // Collect CSSOM declarations.
                IReadOnlyList<CssDeclaration> declarations = CollectDeclarations(styleRule.Style);

                // Step B: Supplement with any dropped properties recovered from the raw text pass.
                string[] simpleSelectors = SplitGroupedSelector(styleRule.SelectorText);

                foreach (string simpleSelector in simpleSelectors)
                {
                    string selectorKey = simpleSelector.Trim();
                    List<CssDeclaration> merged = declarations.ToList();

                    if (supplemental.TryGetValue(selectorKey, out var extra))
                    {
                        // Merge only properties not already present from the CSSOM.
                        var alreadyPresent = new HashSet<string>(
                            merged.Select(d => d.Property), StringComparer.OrdinalIgnoreCase);
                        foreach (var decl in extra)
                        {
                            if (!alreadyPresent.Contains(decl.Property))
                                merged.Add(decl);
                        }
                    }

                    // Specificity strategy:
                    // • If the rule has a single (non-grouped) selector, ICssStyleRule.Selector is
                    //   a single ISelector whose Specificity (AngleSharp.Css.Priority) is exact.
                    //   We read it and pack as ids*10000 + classes*100 + tags.
                    // • For grouped rules (split selectors), the ISelector is a list selector and
                    //   does not have per-simple-selector specificity; fall back to manual CSS 2.1
                    //   §6.4.3 computation via the split string.
                    int specificity = simpleSelectors.Length == 1
                        ? ComputeSpecificityFromSelector(styleRule.Selector)
                        : ComputeSpecificityFromText(selectorKey);

                    result.Add(new CssMatchableRule(
                        SelectorText: selectorKey,
                        Specificity: specificity,
                        SourceOrder: sourceOrder++,
                        Declarations: merged));
                }
            }
        }

        // Step C: Also add rules that only appear in the raw-text supplemental pass but NOT in the
        // CSSOM (possible if AngleSharp.Css dropped an entire rule — unlikely but defensive).
        // We do this only for selector-matched rules already present (CSSOM anchor is required for
        // specificity; pure raw-text rules without CSSOM anchor are out of scope).

        return new CssRuleSet(result);
    }

    // -----------------------------------------------------------------------
    // Supplemental raw-text parser for AngleSharp.Css-dropped properties
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks every <c>&lt;style&gt;</c> element in the document, parses its raw text content,
    /// and returns a map from selector text → supplemental <see cref="CssDeclaration"/> list
    /// for properties in <see cref="SupplementalProperties"/> that the CSSOM drops.
    /// </summary>
    private static Dictionary<string, List<CssDeclaration>> BuildSupplementalFromRawStyleText(
        IDocument document)
    {
        var map = new Dictionary<string, List<CssDeclaration>>(StringComparer.Ordinal);

        // Walk all <style> elements in the document (handles both <head> and <body> positions).
        foreach (IElement styleEl in document.GetElementsByTagName("style"))
        {
            string? rawText = styleEl.TextContent;
            if (string.IsNullOrWhiteSpace(rawText))
                continue;

            ParseRawCssForSupplemental(rawText, map);
        }

        return map;
    }

    /// <summary>
    /// Minimal raw CSS parser: extracts selector + declaration blocks, then for each block
    /// collects only the properties listed in <see cref="SupplementalProperties"/>.
    /// Handles nested braces (e.g. @media) by only parsing top-level rules.
    /// </summary>
    private static void ParseRawCssForSupplemental(
        string css,
        Dictionary<string, List<CssDeclaration>> map)
    {
        int pos = 0;
        int len = css.Length;

        while (pos < len)
        {
            // Skip whitespace and comments.
            pos = SkipWhitespace(css, pos);
            if (pos >= len) break;

            // Skip /* ... */ comments.
            if (pos + 1 < len && css[pos] == '/' && css[pos + 1] == '*')
            {
                int end = css.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                pos = end >= 0 ? end + 2 : len;
                continue;
            }

            // Skip @-rules (e.g. @media, @import, @page) — find the matching { } or ';'.
            if (pos < len && css[pos] == '@')
            {
                int semi = css.IndexOf(';', pos);
                int brace = css.IndexOf('{', pos);
                if (brace < 0 || (semi >= 0 && semi < brace))
                {
                    pos = semi >= 0 ? semi + 1 : len;
                }
                else
                {
                    // Skip balanced { }
                    pos = SkipBalancedBraces(css, brace);
                }
                continue;
            }

            // Read selector text (up to the opening '{')
            int selectorStart = pos;
            int openBrace = css.IndexOf('{', pos);
            if (openBrace < 0) break;

            string selectorText = css.Substring(selectorStart, openBrace - selectorStart).Trim();
            if (string.IsNullOrEmpty(selectorText))
            {
                pos = openBrace + 1;
                continue;
            }

            // Read declaration block (between { and the matching })
            int closeIdx = FindMatchingClose(css, openBrace);
            string declBlock = closeIdx > openBrace
                ? css.Substring(openBrace + 1, closeIdx - openBrace - 1)
                : string.Empty;
            pos = closeIdx >= 0 ? closeIdx + 1 : len;

            // Extract supplemental declarations from this block.
            List<CssDeclaration>? supplementalDecls = ExtractSupplementalDecls(declBlock);
            if (supplementalDecls is null || supplementalDecls.Count == 0)
                continue;

            // Split grouped selectors and record under each simple selector.
            string[] simpleSelectors = SplitGroupedSelector(selectorText);
            foreach (string simple in simpleSelectors)
            {
                string key = simple.Trim();
                if (!map.TryGetValue(key, out List<CssDeclaration>? existing))
                {
                    map[key] = supplementalDecls;
                }
                else
                {
                    // Merge: don't overwrite already-present properties from earlier rules.
                    var existingProps = new HashSet<string>(
                        existing.Select(d => d.Property), StringComparer.OrdinalIgnoreCase);
                    foreach (var decl in supplementalDecls)
                    {
                        if (!existingProps.Contains(decl.Property))
                            existing.Add(decl);
                    }
                }
            }
        }
    }

    private static List<CssDeclaration>? ExtractSupplementalDecls(string declBlock)
    {
        List<CssDeclaration>? result = null;
        foreach (string decl in declBlock.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = decl.IndexOf(':');
            if (colon <= 0) continue;

            string prop = decl[..colon].Trim().ToLowerInvariant();
            if (!SupplementalProperties.Contains(prop)) continue;

            string val = decl[(colon + 1)..].Trim();
            if (string.IsNullOrEmpty(val)) continue;

            bool imp = val.EndsWith("!important", StringComparison.OrdinalIgnoreCase);
            if (imp) val = val[..^"!important".Length].Trim();
            if (string.IsNullOrEmpty(val)) continue;

            result ??= new List<CssDeclaration>();
            result.Add(new CssDeclaration(prop, val, imp));
        }
        return result;
    }

    private static int SkipWhitespace(string s, int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        return pos;
    }

    private static int FindMatchingClose(string s, int openIdx)
    {
        int depth = 0;
        for (int i = openIdx; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static int SkipBalancedBraces(string s, int openIdx)
    {
        int close = FindMatchingClose(s, openIdx);
        return close >= 0 ? close + 1 : s.Length;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Properties that AngleSharp.Css beta.147 silently drops from <c>ICssStyleDeclaration</c>
    /// because they are CSS3 properties not yet fully implemented in the beta parser.
    /// We supplement CSSOM collection with raw-text parsing for these properties so the
    /// cascade can resolve them correctly (otherwise G28/G29 word-break/white-space would
    /// never reach the resolver).
    /// </summary>
    private static readonly HashSet<string> SupplementalProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "word-break",
        "overflow-wrap",
        "word-wrap",
        "white-space",
        "text-overflow",
        "hyphens",
    };

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
