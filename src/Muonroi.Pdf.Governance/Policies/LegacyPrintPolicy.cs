using AngleSharp.Css.Dom;
using Muonroi.Pdf.Governance.Cascade;

namespace Muonroi.Pdf.Governance.Policies;

/// <summary>
/// Profile v1 gate for Legacy Print-HTML rendering.
/// <para>
/// Extends the safe CSS 2.1 print subset relative to <see cref="DefaultStrictPolicy"/>:
/// <list type="bullet">
///   <item>ALLOWS: <c>float:left/right</c>, <c>position:absolute</c>, <c>border-collapse:collapse</c></item>
///   <item>STILL BLOCKS: <c>display:flex/grid</c>, <c>position:fixed/sticky</c>, <c>@keyframes</c>,
///     external <c>@import</c>, <c>&lt;script&gt;</c> elements, <c>javascript:/file:</c> href schemes,
///     geometric CSS transforms, and background gradients.</item>
/// </list>
/// </para>
/// <para>Limits are identical to <see cref="PdfPolicyLimits.Strict"/> (element count, DOM depth, HTML bytes).</para>
/// </summary>
public sealed class LegacyPrintPolicy : IPdfCssPolicy
{
    public string Id => "legacy-print-v1";
    public PdfPolicyLimits Limits => PdfPolicyLimits.Strict;

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

        // Pass 2: Computed style per element.
        // Profile v1 ALLOWS: float:left/right, position:absolute, border-collapse:collapse.
        // Profile v1 BLOCKS: display:flex/grid, position:fixed/sticky, geometric transforms, gradients.
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
            string position = style.GetPropertyValue("position") ?? string.Empty;

            if (display is "flex" or "inline-flex")
                violations.Add(ViolationFor("forbidden.display.flex", "display", display, selector, "display:block"));
            if (display is "grid" or "inline-grid")
                violations.Add(ViolationFor("forbidden.display.grid", "display", display, selector, "display:table"));

            // position:absolute is ALLOWED in Profile v1 (CSS 2.1 print layout).
            // position:fixed and position:sticky remain blocked.
            if (position is "fixed")
                violations.Add(ViolationFor("forbidden.position.fixed", "position", position, selector, "position:static"));
            if (position is "sticky")
                violations.Add(ViolationFor("forbidden.position.sticky", "position", position, selector, "position:static"));

            // float:left/right and border-collapse:collapse are ALLOWED in Profile v1.

            // New in Profile v1: block geometric CSS transforms (non-print-safe).
            string transform = style.GetPropertyValue("transform") ?? string.Empty;
            if (!string.IsNullOrEmpty(transform))
                violations.Add(ViolationFor("forbidden.transform.geometric", "transform", transform, selector, "Remove transform property"));

            // New in Profile v1: block background gradients (not renderable by the engine).
            string background = style.GetPropertyValue("background") ?? string.Empty;
            string backgroundImage = style.GetPropertyValue("background-image") ?? string.Empty;
            if (background.Contains("gradient", StringComparison.OrdinalIgnoreCase) ||
                backgroundImage.Contains("gradient", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(ViolationFor("forbidden.background.gradient", "background",
                    background.Contains("gradient", StringComparison.OrdinalIgnoreCase) ? background : backgroundImage,
                    selector, "Use a solid background-color instead"));
            }
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
                break;
            }
        }

        // Pass 3 (continued): dangerous href schemes
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
