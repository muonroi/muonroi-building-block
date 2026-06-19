using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

namespace Muonroi.Pdf.Governance.Cascade;

/// <summary>
/// Resolves one HTML element against a <see cref="CssRuleSet"/> into a complete CSS property map
/// for the Legacy Print-HTML Profile v1 surface.
///
/// <para>The 7-step algorithm (DESIGN §4.2):</para>
/// <list type="number">
///   <item>Match: filter rules by <c>element.Matches(selectorText)</c> (AngleSharp core — non-throwing).</item>
///   <item>Sort matched ascending by (Important, Specificity, SourceOrder); apply declarations last-wins.</item>
///   <item>Inline overlay: <c>style=""</c> attribute declarations on top of author rules.</item>
///   <item>Shorthand expansion: border, border-{side}, margin, padding, background, font, text-decoration.</item>
///   <item>UA defaults: HTML5 display map; th/h1–h6 bold; b/strong/i/em/u; hr.</item>
///   <item>Inheritance: copy inherited properties from parent's resolved map where still unset.</item>
///   <item>Unit resolution: em/rem → px; % left literal; px/pt/mm/cm pass through.</item>
/// </list>
///
/// <para>Does NOT call <c>IWindow.GetComputedStyle</c> or <c>ComputeCurrentStyle</c> anywhere.</para>
/// </summary>
internal sealed class CascadeResolver
{
    // -----------------------------------------------------------------------
    // Module tag for logging (No-Silent-Catch rule)
    // -----------------------------------------------------------------------
    private const string Module = "[CascadeResolver]";

    private readonly CssRuleSet _ruleSet;
    private readonly ILogger? _logger;

    internal CascadeResolver(CssRuleSet ruleSet, ILogger? logger = null)
    {
        _ruleSet = ruleSet;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Step 6 — Inherited property allow-list (CSS 2.1 inherited set ∩ Profile v1)
    // -----------------------------------------------------------------------
    private static readonly HashSet<string> InheritedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "color",
        "font-family",
        "font-size",
        "font-weight",
        "font-style",
        "font-variant",
        "line-height",
        "letter-spacing",
        "word-spacing",
        "text-align",
        "text-transform",
        "text-indent",
        "white-space",
        "word-break",
        "overflow-wrap",
        "word-wrap",
        "visibility",
        "list-style",
        "list-style-type",
        "list-style-position",
        "list-style-image",
        "cursor",
        "direction",
    };

    // -----------------------------------------------------------------------
    // Step 5 — UA display map for HTML5 table structural elements
    // -----------------------------------------------------------------------
    private static readonly Dictionary<string, string> UaDisplayMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["table"]   = "table",
            ["thead"]   = "table-header-group",
            ["tbody"]   = "table-row-group",
            ["tfoot"]   = "table-footer-group",
            ["tr"]      = "table-row",
            ["td"]      = "table-cell",
            ["th"]      = "table-cell",
            ["caption"] = "table-caption",
        };

    // -----------------------------------------------------------------------
    // Step 5 — UA inline tags (need not be listed in display map, just recorded)
    // -----------------------------------------------------------------------
    private static readonly HashSet<string> UaInlineTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "span", "label", "a", "strong", "em", "b", "i", "u",
        "code", "kbd", "mark", "small", "sub", "sup", "time",
        "cite", "abbr", "q", "var", "samp", "dfn",
        "tt", "s", "del", "ins", "bdo", "bdi", "ruby", "rt",
    };

    // -----------------------------------------------------------------------
    // Step 5 — UA block tags not otherwise in the display map
    // -----------------------------------------------------------------------
    private static readonly HashSet<string> UaBlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "div", "p", "section", "article", "aside", "main", "header", "footer", "nav",
        "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li",
        "blockquote", "figure", "figcaption", "address", "pre", "hr",
        "form", "fieldset", "details", "summary",
    };

    // -----------------------------------------------------------------------
    // Root default font-size in px (CSS initial value)
    // -----------------------------------------------------------------------
    private const float RootFontSizePx = 16f;

    // -----------------------------------------------------------------------
    // Public resolve entry point
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the CSS property map for <paramref name="element"/> by running the full 7-step
    /// cascade algorithm against the <see cref="CssRuleSet"/> this resolver was built with.
    /// </summary>
    /// <param name="element">The DOM element to resolve styles for.</param>
    /// <param name="parentResolved">
    /// The already-resolved map of the parent element, used for inheritance in step 6.
    /// Pass <see langword="null"/> for the root element.
    /// </param>
    /// <returns>
    /// A case-insensitive dictionary mapping lowercased CSS property names to resolved values.
    /// </returns>
    internal Dictionary<string, string> Resolve(
        IElement element,
        IReadOnlyDictionary<string, string>? parentResolved)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // -------------------------------------------------------------------
        // Step 1+2: Match rules via element.Matches, sort, apply last-wins.
        // -------------------------------------------------------------------
        ApplyAuthorRules(element, map);

        // -------------------------------------------------------------------
        // Step 3: Inline style= overlay
        // -------------------------------------------------------------------
        ApplyInlineStyle(element, map);

        // -------------------------------------------------------------------
        // Step 4: Shorthand expansion (into longhands the layout engine reads)
        // -------------------------------------------------------------------
        ExpandShorthands(map);

        // -------------------------------------------------------------------
        // Step 5: UA defaults (only for properties not yet set)
        // -------------------------------------------------------------------
        ApplyUaDefaults(element, map);

        // -------------------------------------------------------------------
        // Step 6: Inheritance
        // -------------------------------------------------------------------
        ApplyInheritance(map, parentResolved);

        // -------------------------------------------------------------------
        // Step 7: Unit resolution (em/rem → px string; % left as-is)
        // -------------------------------------------------------------------
        ResolveUnits(map);

        return map;
    }

    // -----------------------------------------------------------------------
    // Step 1+2 — Match author rules; sort; apply into map (last-wins)
    // -----------------------------------------------------------------------

    private void ApplyAuthorRules(IElement element, Dictionary<string, string> map)
    {
        // Separate important from non-important matched declarations.
        // Non-important sorted by (specificity, sourceOrder) ascending.
        // Important layer on top, same sort.
        var nonImportant = new List<(int Specificity, int SourceOrder, CssDeclaration Decl)>();
        var important    = new List<(int Specificity, int SourceOrder, CssDeclaration Decl)>();

        foreach (CssMatchableRule rule in _ruleSet.Rules)
        {
            bool matched;
            try
            {
                matched = element.Matches(rule.SelectorText);
            }
            catch (Exception ex)
            {
                // No-Silent-Catch: log module + selector + message, then skip this rule.
                _logger?.LogDebug(
                    "{Module} selector-match failed for '{Selector}': {Message}",
                    Module, rule.SelectorText, ex.Message);
                continue;
            }

            if (!matched)
                continue;

            foreach (CssDeclaration decl in rule.Declarations)
            {
                if (decl.Important)
                    important.Add((rule.Specificity, rule.SourceOrder, decl));
                else
                    nonImportant.Add((rule.Specificity, rule.SourceOrder, decl));
            }
        }

        // Sort ascending — last entry wins (apply in order, later overwrites earlier).
        nonImportant.Sort(Compare);
        important.Sort(Compare);

        foreach (var (_, _, decl) in nonImportant)
            map[decl.Property] = decl.Value;

        // Important layer overwrites non-important.
        foreach (var (_, _, decl) in important)
            map[decl.Property] = decl.Value;
    }

    private static int Compare(
        (int Specificity, int SourceOrder, CssDeclaration _) a,
        (int Specificity, int SourceOrder, CssDeclaration _) b)
    {
        int cmp = a.Specificity.CompareTo(b.Specificity);
        return cmp != 0 ? cmp : a.SourceOrder.CompareTo(b.SourceOrder);
    }

    // -----------------------------------------------------------------------
    // Step 3 — Inline style= overlay
    // -----------------------------------------------------------------------

    private void ApplyInlineStyle(IElement element, Dictionary<string, string> map)
    {
        string? styleAttr = element.GetAttribute("style");
        if (string.IsNullOrWhiteSpace(styleAttr))
            return;

        var inlineNormal    = new List<(string Property, string Value)>();
        var inlineImportant = new List<(string Property, string Value)>();

        // Simple k:v; splitter — sufficient for the inline style="" attribute format.
        foreach (string decl in styleAttr.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = decl.IndexOf(':');
            if (colon <= 0)
                continue;

            string prop  = decl[..colon].Trim().ToLowerInvariant();
            string value = decl[(colon + 1)..].Trim();
            if (string.IsNullOrEmpty(prop) || string.IsNullOrEmpty(value))
                continue;

            bool imp = value.EndsWith("!important", StringComparison.OrdinalIgnoreCase);
            if (imp)
                value = value[..^"!important".Length].Trim();

            if (imp)
                inlineImportant.Add((prop, value));
            else
                inlineNormal.Add((prop, value));
        }

        // Inline normal overlays author non-important (already in map).
        foreach (var (prop, value) in inlineNormal)
            map[prop] = value;

        // Inline important beats everything.
        foreach (var (prop, value) in inlineImportant)
            map[prop] = value;
    }

    // -----------------------------------------------------------------------
    // Step 4 — Shorthand expansion
    // -----------------------------------------------------------------------

    private static void ExpandShorthands(Dictionary<string, string> map)
    {
        // Process each shorthand if it is present in the map.
        // Each expansion method removes the shorthand key and writes longhands
        // only when the longhand is not already set (author longhands win).

        if (map.TryGetValue("border", out string? borderAll))
        {
            map.Remove("border");
            ExpandBorderShorthand(borderAll, map,
                "border-top", "border-right", "border-bottom", "border-left");
        }

        // border-{side} shorthands
        foreach (string side in new[] { "border-top", "border-right", "border-bottom", "border-left" })
        {
            if (map.TryGetValue(side, out string? sideVal))
            {
                map.Remove(side);
                ExpandBorderSideShorthand(side, sideVal, map);
            }
        }

        // margin
        if (map.TryGetValue("margin", out string? margin))
        {
            map.Remove("margin");
            ExpandFourSides("margin", margin, map);
        }

        // padding
        if (map.TryGetValue("padding", out string? padding))
        {
            map.Remove("padding");
            ExpandFourSides("padding", padding, map);
        }

        // background
        if (map.TryGetValue("background", out string? bg))
        {
            map.Remove("background");
            ExpandBackground(bg, map);
        }

        // font
        if (map.TryGetValue("font", out string? font))
        {
            map.Remove("font");
            ExpandFont(font, map);
        }

        // text-decoration
        if (map.TryGetValue("text-decoration", out string? td))
        {
            // text-decoration is treated as a longhand itself for our profile;
            // just normalize the value. Keep the key.
            map["text-decoration"] = td.Trim().ToLowerInvariant();
        }
    }

    /// <summary>
    /// Expands <c>border: [width] [style] [color]</c> or <c>border: none</c>/<c>border: 0</c>
    /// into all four border sides' width/style/color longhands.
    /// </summary>
    private static void ExpandBorderShorthand(
        string value,
        Dictionary<string, string> map,
        params string[] sides)
    {
        string v = value.Trim().ToLowerInvariant();

        if (v is "none" or "0" or "hidden")
        {
            // border: none → all four sides width=0, style=none
            foreach (string side in sides)
            {
                SetIfAbsent(map, $"{side}-width", "0");
                SetIfAbsent(map, $"{side}-style", "none");
            }
            return;
        }

        // Parse "width style color" tokens (CSS §8.5.4)
        ParseBorderTokens(v, out string? width, out string? style, out string? color);

        foreach (string side in sides)
        {
            if (width is not null)  SetIfAbsent(map, $"{side}-width", width);
            if (style is not null)  SetIfAbsent(map, $"{side}-style", style);
            if (color is not null)  SetIfAbsent(map, $"{side}-color", color);
        }
    }

    /// <summary>
    /// Expands <c>border-{side}: [width] [style] [color]</c> into three longhands.
    /// </summary>
    private static void ExpandBorderSideShorthand(
        string side,
        string value,
        Dictionary<string, string> map)
    {
        string v = value.Trim().ToLowerInvariant();

        if (v is "none" or "0" or "hidden")
        {
            SetIfAbsent(map, $"{side}-width", "0");
            SetIfAbsent(map, $"{side}-style", "none");
            return;
        }

        ParseBorderTokens(v, out string? width, out string? style, out string? color);
        if (width is not null) SetIfAbsent(map, $"{side}-width", width);
        if (style is not null) SetIfAbsent(map, $"{side}-style", style);
        if (color is not null) SetIfAbsent(map, $"{side}-color", color);
    }

    private static readonly HashSet<string> BorderStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "hidden", "dotted", "dashed", "solid", "double",
        "groove", "ridge", "inset", "outset",
    };

    private static void ParseBorderTokens(
        string value,
        out string? width,
        out string? style,
        out string? color)
    {
        width = style = color = null;
        string[] tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string tok in tokens)
        {
            if (BorderStyles.Contains(tok))
            {
                style ??= tok;
            }
            else if (IsLengthValue(tok))
            {
                width ??= tok;
            }
            else
            {
                // Assume it's a color value.
                color ??= tok;
            }
        }
    }

    private static bool IsLengthValue(string tok)
    {
        // Quick check: ends with a known unit suffix or is a numeric-ish string.
        return tok.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            || tok.EndsWith("em", StringComparison.OrdinalIgnoreCase)
            || tok.EndsWith("rem", StringComparison.OrdinalIgnoreCase)
            || tok.EndsWith("pt", StringComparison.OrdinalIgnoreCase)
            || tok.EndsWith("mm", StringComparison.OrdinalIgnoreCase)
            || tok.EndsWith("cm", StringComparison.OrdinalIgnoreCase)
            || tok == "0"
            || tok == "thin"
            || tok == "medium"
            || tok == "thick";
    }

    /// <summary>
    /// Expands a CSS 1/2/3/4-value shorthand (margin, padding) into four longhands.
    /// </summary>
    private static void ExpandFourSides(string prefix, string value, Dictionary<string, string> map)
    {
        string[] tokens = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string top, right, bottom, left;

        switch (tokens.Length)
        {
            case 1:
                top = right = bottom = left = tokens[0];
                break;
            case 2:
                top    = bottom = tokens[0];
                right  = left   = tokens[1];
                break;
            case 3:
                top    = tokens[0];
                right  = left  = tokens[1];
                bottom = tokens[2];
                break;
            default: // 4+
                top    = tokens[0];
                right  = tokens[1];
                bottom = tokens[2];
                left   = tokens[3];
                break;
        }

        SetIfAbsent(map, $"{prefix}-top",    top);
        SetIfAbsent(map, $"{prefix}-right",  right);
        SetIfAbsent(map, $"{prefix}-bottom", bottom);
        SetIfAbsent(map, $"{prefix}-left",   left);
    }

    /// <summary>
    /// Minimal background shorthand expansion — extracts color for the profile surface.
    /// Full background parsing is complex; for the bounded profile surface, only
    /// background-color and background-image are consumed.
    /// </summary>
    private static void ExpandBackground(string value, Dictionary<string, string> map)
    {
        string v = value.Trim();
        // If it looks like a plain color (no url(...)), use as background-color.
        if (!v.StartsWith("url(", StringComparison.OrdinalIgnoreCase)
            && !v.Contains("gradient", StringComparison.OrdinalIgnoreCase))
        {
            SetIfAbsent(map, "background-color", v);
        }
    }

    /// <summary>
    /// Minimal font shorthand expansion covering the common "style weight size/line-height family" form.
    /// E.g.: "bold 12px Arial" → font-weight:bold, font-size:12px, font-family:Arial
    /// </summary>
    private static void ExpandFont(string value, Dictionary<string, string> map)
    {
        // font shorthand is complex; cover the common cases seen in legacy print HTML.
        // Format: [style] [variant] [weight] <size>[/<line-height>] <family>
        // We extract the tokens we recognize and leave the rest.
        string[] tokens = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var styleKeywords  = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "italic", "oblique", "normal" };
        var weightKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "bold", "bolder", "lighter", "100", "200", "300", "400", "500", "600", "700", "800", "900" };
        var variantKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "small-caps" };

        var familyParts = new List<string>();
        bool sizeFound = false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string tok = tokens[i];

            if (!sizeFound && styleKeywords.Contains(tok))
            {
                SetIfAbsent(map, "font-style", tok);
                continue;
            }
            if (!sizeFound && variantKeywords.Contains(tok))
                continue; // skip font-variant

            if (!sizeFound && weightKeywords.Contains(tok))
            {
                SetIfAbsent(map, "font-weight", tok);
                continue;
            }

            if (!sizeFound && (IsLengthValue(tok) || tok.Contains('/')))
            {
                // Size / line-height
                string[] parts = tok.Split('/');
                SetIfAbsent(map, "font-size", parts[0]);
                if (parts.Length > 1)
                    SetIfAbsent(map, "line-height", parts[1]);
                sizeFound = true;
                continue;
            }

            if (sizeFound)
                familyParts.Add(tok.Trim(','));
        }

        if (familyParts.Count > 0)
            SetIfAbsent(map, "font-family", string.Join(" ", familyParts));
    }

    private static void SetIfAbsent(Dictionary<string, string> map, string key, string value)
    {
        if (!map.ContainsKey(key))
            map[key] = value;
    }

    // -----------------------------------------------------------------------
    // Step 5 — UA defaults layer (lowest precedence — only fill gaps)
    // -----------------------------------------------------------------------

    private static void ApplyUaDefaults(IElement element, Dictionary<string, string> map)
    {
        string tag = element.LocalName?.ToLowerInvariant() ?? "";

        // Display map for table structural elements and inline/block defaults.
        if (!map.ContainsKey("display"))
        {
            if (UaDisplayMap.TryGetValue(tag, out string? displayVal))
                map["display"] = displayVal;
            else if (UaInlineTags.Contains(tag))
                map["display"] = "inline";
            else if (UaBlockTags.Contains(tag))
                map["display"] = "block";
        }

        // th → font-weight: bold + text-align: center
        if (tag == "th")
        {
            if (!map.ContainsKey("font-weight"))
                map["font-weight"] = "bold";
            if (!map.ContainsKey("text-align"))
                map["text-align"] = "center";
        }

        // h1–h6 → font-weight: bold
        if (tag is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
        {
            if (!map.ContainsKey("font-weight"))
                map["font-weight"] = "bold";
        }

        // b, strong → font-weight: bold
        if (tag is "b" or "strong")
        {
            if (!map.ContainsKey("font-weight"))
                map["font-weight"] = "bold";
        }

        // i, em → font-style: italic
        if (tag is "i" or "em")
        {
            if (!map.ContainsKey("font-style"))
                map["font-style"] = "italic";
        }

        // u → text-decoration: underline
        if (tag == "u")
        {
            if (!map.ContainsKey("text-decoration"))
                map["text-decoration"] = "underline";
        }

        // hr → display: block (if not already set)
        if (tag == "hr" && !map.ContainsKey("display"))
            map["display"] = "block";
    }

    // -----------------------------------------------------------------------
    // Step 6 — Inheritance
    // -----------------------------------------------------------------------

    private static void ApplyInheritance(
        Dictionary<string, string> map,
        IReadOnlyDictionary<string, string>? parentResolved)
    {
        if (parentResolved is null)
            return;

        foreach (string prop in InheritedProperties)
        {
            if (!map.ContainsKey(prop) && parentResolved.TryGetValue(prop, out string? parentVal)
                && !string.IsNullOrEmpty(parentVal))
            {
                map[prop] = parentVal;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Step 7 — Unit resolution (em/rem → px string literal; % unchanged)
    // -----------------------------------------------------------------------

    private static void ResolveUnits(Dictionary<string, string> map)
    {
        // Determine effective font-size in px (for em resolution).
        float fontSizePx = RootFontSizePx;
        if (map.TryGetValue("font-size", out string? fsSrc) && !string.IsNullOrEmpty(fsSrc))
        {
            float parsed = ParseLengthToPx(fsSrc, RootFontSizePx);
            if (parsed > 0f) fontSizePx = parsed;
        }

        // Properties that accept length values and need em/rem resolved.
        // % values are left as literal strings for layout to interpret.
        string[] lengthProperties =
        [
            "width", "height", "min-width", "max-width",
            "margin-top", "margin-right", "margin-bottom", "margin-left",
            "padding-top", "padding-right", "padding-bottom", "padding-left",
            "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
            "font-size",
            "line-height",
            "top", "left", "right", "bottom",
            "border-spacing",
        ];

        foreach (string prop in lengthProperties)
        {
            if (!map.TryGetValue(prop, out string? raw) || string.IsNullOrEmpty(raw))
                continue;

            string resolved = ResolveUnitValue(raw, fontSizePx);
            if (resolved != raw)
                map[prop] = resolved;
        }
    }

    /// <summary>
    /// Converts em/rem length values to px strings. Leaves %, px, pt, and other values unchanged.
    /// Returns the original value on any parse failure (defensive — no throw, T-12-05).
    /// </summary>
    private static string ResolveUnitValue(string value, float fontSizePx)
    {
        ReadOnlySpan<char> span = value.AsSpan().Trim();

        if (span.EndsWith("em", StringComparison.Ordinal)
            && !span.EndsWith("rem", StringComparison.Ordinal))
        {
            if (TryParseFloat(span[..^2], out float em))
                return $"{em * fontSizePx:0.####}px";
        }
        else if (span.EndsWith("rem", StringComparison.Ordinal))
        {
            if (TryParseFloat(span[..^3], out float rem))
                return $"{rem * RootFontSizePx:0.####}px";
        }

        // % and all other units pass through unchanged.
        return value;
    }

    /// <summary>
    /// Parses a CSS length value to px (for internal font-size resolution only).
    /// Returns 0 on failure or when the value is % (cannot resolve without containing block).
    /// </summary>
    private static float ParseLengthToPx(string value, float emBase)
    {
        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.IsEmpty || span.SequenceEqual("auto") || span.SequenceEqual("normal"))
            return 0f;

        if (span.EndsWith("%", StringComparison.Ordinal))
            return 0f; // Cannot resolve without a containing block — leave as literal.

        if (span.EndsWith("px", StringComparison.Ordinal))
            return TryParseFloat(span[..^2], out float px) ? px : 0f;

        if (span.EndsWith("pt", StringComparison.Ordinal))
            // 1pt ≈ 1.333px (96dpi screen: 1px = 0.75pt → 1pt = 1.333px)
            return TryParseFloat(span[..^2], out float pt) ? pt * 4f / 3f : 0f;

        if (span.EndsWith("em", StringComparison.Ordinal)
            && !span.EndsWith("rem", StringComparison.Ordinal))
            return TryParseFloat(span[..^2], out float em) ? em * emBase : 0f;

        if (span.EndsWith("rem", StringComparison.Ordinal))
            return TryParseFloat(span[..^3], out float rem) ? rem * RootFontSizePx : 0f;

        if (span.EndsWith("mm", StringComparison.Ordinal))
            // 1mm ≈ 3.7795px
            return TryParseFloat(span[..^2], out float mm) ? mm * 3.7795f : 0f;

        if (span.EndsWith("cm", StringComparison.Ordinal))
            return TryParseFloat(span[..^2], out float cm) ? cm * 37.795f : 0f;

        // Bare number — treat as px
        return TryParseFloat(span, out float bare) ? bare : 0f;
    }

    private static bool TryParseFloat(ReadOnlySpan<char> span, out float result)
    {
        return float.TryParse(
            span,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
    }
}
