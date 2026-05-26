using AngleSharp.Css.Dom;
using Muonroi.Pdf.Governance.Cascade;

namespace Muonroi.Pdf.Governance.Policies;

public sealed class DefaultStrictPolicy : IPdfCssPolicy
{
    public string Id => "default-strict-v1";
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

        // Pass 2: Computed style per element — display, float, position
        IWindow? defaultView = document.DefaultView;
        if (defaultView is null) return;

        foreach (IElement element in document.All.OfType<IElement>())
        {
            ICssStyleDeclaration? style = defaultView.GetComputedStyle(element);
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
