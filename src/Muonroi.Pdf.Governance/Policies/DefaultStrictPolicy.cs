using AngleSharp.Css.Dom;
using Muonroi.Pdf.Governance.Cascade;

namespace Muonroi.Pdf.Governance.Policies;

/// <summary>
/// The default strict CSS policy for PDF rendering. Blocks all CSS features that are
/// unsafe or unsupported in the print renderer: external <c>@import</c>, <c>@keyframes</c>,
/// CSS transitions, <c>display:flex/grid</c>, <c>float</c>, <c>position:absolute/fixed/sticky</c>,
/// <c>border-collapse:collapse</c>, <c>&lt;script&gt;</c> elements, and non-http(s)/mailto
/// link schemes. Enforces <see cref="PdfPolicyLimits.Strict"/> structural limits.
/// </summary>
public sealed class DefaultStrictPolicy : IPdfCssPolicy
{
    /// <summary>Gets the stable identifier for this policy version.</summary>
    public string Id => "default-strict-v1";

    /// <summary>Gets the structural limits (element count, DOM depth, HTML bytes) enforced by this policy.</summary>
    public PdfPolicyLimits Limits => PdfPolicyLimits.Strict;

    /// <summary>
    /// Validates <paramref name="context"/> against the strict CSS policy rules.
    /// Checks structural limits and, when the context is an <see cref="AngleSharpStyledDocument"/>,
    /// walks the stylesheet AST and computed element styles for forbidden CSS features.
    /// </summary>
    /// <param name="context">The styled document context to validate.</param>
    /// <param name="ct">Cancellation token (unused; reserved for interface conformance).</param>
    /// <returns>
    /// <see cref="PolicyValidationResult.Ok"/> when no violations are found; otherwise a
    /// failed <see cref="PolicyValidationResult"/> listing all detected violations.
    /// </returns>
    public ValueTask<PolicyValidationResult> ValidateAsync(
        IPdfDocumentContext context,
        CancellationToken ct = default)
    {
        var violations = new List<PolicyViolation>();

        CheckLimits(context, violations);

        if (context is AngleSharpStyledDocument styledDoc)
            CheckCssFeatures(styledDoc.AngleSharpDocument, violations);

        return ValueTask.FromResult(violations.Count == 0
            ? PolicyValidationResult.Ok
            : new PolicyValidationResult(false, violations));
    }

    private void CheckLimits(IPdfDocumentContext context, List<PolicyViolation> violations)
    {
        if (context.ElementCount > Limits.MaxElementCount)
            violations.Add(new PolicyViolation(
                "limit.max-element-count",
                $"Element count {context.ElementCount} exceeds limit {Limits.MaxElementCount}."));

        if (context.MaxDepth > Limits.MaxDomDepth)
            violations.Add(new PolicyViolation(
                "limit.max-dom-depth",
                $"DOM depth {context.MaxDepth} exceeds limit {Limits.MaxDomDepth}."));

        if (context.SourceHtmlBytes > Limits.MaxHtmlBytes)
            violations.Add(new PolicyViolation(
                "limit.max-html-bytes",
                $"Source HTML {context.SourceHtmlBytes} bytes exceeds limit {Limits.MaxHtmlBytes}."));
    }

    private static void CheckCssFeatures(IDocument document, List<PolicyViolation> violations)
    {
        // Pass 1: Stylesheet AST walk — @import external, @keyframes, transition
        foreach (ICssStyleSheet sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            ICssRuleList rules = sheet.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                ICssRule rule = rules[i];

                if (rule is ICssImportRule importRule && IsExternalUri(importRule.Href))
                {
                    violations.Add(new PolicyViolation(
                        "forbidden.import.external",
                        $"External @import is not allowed: {importRule.Href}",
                        PropertyName: "@import",
                        CssSelector: "@import",
                        RejectedValue: importRule.Href,
                        SuggestedAlternative: "Inline the stylesheet"));
                }
                else if (rule is ICssKeyframesRule keyframesRule)
                {
                    violations.Add(new PolicyViolation(
                        "forbidden.css-animation",
                        $"CSS animations (@keyframes) are not supported: {keyframesRule.Name}",
                        PropertyName: "animation",
                        CssSelector: $"@keyframes {keyframesRule.Name}",
                        RejectedValue: $"@keyframes {keyframesRule.Name}",
                        SuggestedAlternative: "Remove animation properties"));
                }
                else if (rule is ICssStyleRule styleRule)
                {
                    string? transition = styleRule.Style?.GetPropertyValue("transition");
                    if (!string.IsNullOrEmpty(transition))
                    {
                        violations.Add(new PolicyViolation(
                            "forbidden.css-transition",
                            "CSS transitions are not supported.",
                            PropertyName: "transition",
                            CssSelector: styleRule.SelectorText,
                            RejectedValue: transition,
                            SuggestedAlternative: "Remove transition properties"));
                    }
                }
            }
        }

        // Pass 2: Computed style per element — display, float, position
        IWindow? defaultView = document.DefaultView;
        if (defaultView is null) return;

        foreach (IElement element in document.All.OfType<IElement>())
        {
            ICssStyleDeclaration? style;
            try
            {
                style = defaultView.GetComputedStyle(element);
            }
            catch (ArgumentException)
            {
                // AngleSharp requires a render device to resolve relative units (em, rem, %)
                // in headless (no-browser) contexts. Fall back to reading keyword-based
                // CSS values from the matched author-origin rules via the element's
                // stylesheet-matched styles so that display/float/position are still checked.
                style = null;
                foreach (ICssStyleSheet sheet in element.Owner?.StyleSheets.OfType<ICssStyleSheet>()
                    ?? Enumerable.Empty<ICssStyleSheet>())
                {
                    ICssRuleList rules = sheet.Rules;
                    for (int i = 0; i < rules.Length; i++)
                    {
                        if (rules[i] is ICssStyleRule styleRule && element.Matches(styleRule.SelectorText))
                        {
                            style = styleRule.Style;
                            break;
                        }
                    }

                    if (style is not null) break;
                }
            }

            if (style is null) continue;

            string selector = element.GetSelector() ?? element.LocalName;
            string display = style.GetPropertyValue("display") ?? string.Empty;
            string floatVal = style.GetPropertyValue("float") ?? string.Empty;
            string position = style.GetPropertyValue("position") ?? string.Empty;

            if (display is "flex" or "inline-flex")
                violations.Add(ViolationFor("forbidden.display.flex", "display", display, selector, "display:block"));
            if (display is "grid" or "inline-grid")
                violations.Add(ViolationFor("forbidden.display.grid", "display", display, selector, "display:table"));
            if (floatVal is "left" or "right")
                violations.Add(ViolationFor("forbidden.float", "float", floatVal, selector, "Use display:table layout"));
            if (position is "absolute")
                violations.Add(ViolationFor("forbidden.position.absolute", "position", position, selector, "position:static"));
            if (position is "fixed")
                violations.Add(ViolationFor("forbidden.position.fixed", "position", position, selector, "position:static"));
            if (position is "sticky")
                violations.Add(ViolationFor("forbidden.position.sticky", "position", position, selector, "position:static"));

            string borderCollapse = style.GetPropertyValue("border-collapse") ?? string.Empty;
            if (borderCollapse is "collapse")
                violations.Add(ViolationFor("forbidden.border-collapse.collapse", "border-collapse", borderCollapse, selector, "border-collapse:separate"));
        }

        // Pass 3: HTML element security — reject <script> elements (SEC-05)
        foreach (IElement element in document.All.OfType<IElement>())
        {
            if (element.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new PolicyViolation(
                    "forbidden.script-element",
                    "<script> elements are not permitted in PDF templates.",
                    PropertyName: "script",
                    CssSelector: "script",
                    RejectedValue: "script",
                    SuggestedAlternative: "Remove all <script> elements from the HTML template before rendering"));
                break; // one violation sufficient; don't enumerate all script tags
            }
        }

        // Pass 3 (continued): dangerous href schemes — FIDELITY-12 / SEC-02 adjacent
        // Defense-in-depth: BoxTreeBuilder also filters these at the box-tree level.
        foreach (IElement element in document.All.OfType<IElement>())
        {
            if (!element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase))
                continue;

            string href = element.GetAttribute("href") ?? "";
            if (string.IsNullOrEmpty(href))
                continue;

            string scheme = "";
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
                scheme = uri.Scheme;

            // Empty scheme = relative URL = allowed
            bool allowed = scheme is "" or "http" or "https" or "mailto";
            if (!allowed)
            {
                violations.Add(new PolicyViolation(
                    "forbidden.link.scheme",
                    $"<a> href scheme '{scheme}' is not allowed. Only http, https, and mailto URIs are permitted as link annotations.",
                    RejectedValue: href,
                    PropertyName: "href",
                    CssSelector: "a",
                    SuggestedAlternative: "Use an http or https URI."));
            }
        }
    }

    private static PolicyViolation ViolationFor(
        string ruleId, string property, string value, string selector, string alternative) =>
        new(ruleId,
            $"CSS property '{property}' with value '{value}' is not allowed.",
            PropertyName: property,
            RejectedValue: value,
            CssSelector: selector,
            SuggestedAlternative: alternative);

    private static bool IsExternalUri(string? href) =>
        href is not null &&
        (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         href.StartsWith("//", StringComparison.Ordinal));
}
