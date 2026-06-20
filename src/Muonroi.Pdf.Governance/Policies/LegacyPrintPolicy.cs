using System.Diagnostics;
using System.Diagnostics.Metrics;
using AngleSharp.Css.Dom;
using Microsoft.Extensions.Options;
using Muonroi.Pdf.Abstractions.Telemetry;
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
/// <para>
/// Soft-degrade mode (<see cref="PdfPolicySettings.SoftDegradeUnknownDisplay"/> = <c>true</c>):
/// <c>display:flex/grid</c> and related sub-properties emit <see cref="PolicySeverity.Warning"/>
/// violations instead of errors, and rendering proceeds with the element treated as
/// <c>display:block</c>. Default is strict (opt-in only).
/// </para>
/// <para>Limits are identical to <see cref="PdfPolicyLimits.Strict"/> (element count, DOM depth, HTML bytes).</para>
/// </summary>
public sealed class LegacyPrintPolicy : IPdfCssPolicy
{
    // Flex/grid CSS sub-properties that are dropped silently when soft-degrade is on.
    private static readonly HashSet<string> FlexGridSubProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "flex-grow", "flex-shrink", "flex-basis", "flex-direction", "flex-wrap",
        "justify-content", "align-items", "align-content", "align-self",
        "gap", "row-gap", "column-gap",
        "grid-template-columns", "grid-template-rows", "grid-template-areas", "grid-template",
        "grid-column", "grid-row", "grid-column-start", "grid-column-end",
        "grid-row-start", "grid-row-end", "grid-area",
        "grid-auto-columns", "grid-auto-rows", "grid-auto-flow", "grid"
    };

    // Process-lifetime meter — same source name as PdfMetrics so OtelSetup discovers it.
    // Lazy to avoid Meter allocation when soft-degrade is never used.
    private static readonly Lazy<Counter<long>> _softDegradeCounter = new(() =>
    {
        var meter = new Meter(PdfTelemetryNames.ActivitySourceName);
        return meter.CreateCounter<long>(
            PdfTelemetryNames.PolicySoftDegradeMetric,
            unit: "{page}",
            description: "Counts pages where LegacyPrintPolicy soft-degrade substituted flex/grid as block.");
    });

    private readonly bool _softDegrade;

    /// <summary>
    /// Parameterless constructor — uses default (strict) policy settings.
    /// Existing code that calls <c>new LegacyPrintPolicy()</c> gets unchanged fail-loud behavior.
    /// </summary>
    public LegacyPrintPolicy() : this(softDegrade: false) { }

    /// <summary>
    /// Constructor that reads <see cref="PdfPolicySettings.SoftDegradeUnknownDisplay"/>
    /// from the bound <see cref="PdfConfigs"/> options.
    /// </summary>
    public LegacyPrintPolicy(IOptions<PdfConfigs> options)
        : this(options?.Value?.Policy?.SoftDegradeUnknownDisplay ?? false) { }

    private LegacyPrintPolicy(bool softDegrade)
    {
        _softDegrade = softDegrade;
    }

    /// <summary>Gets the stable identifier for this policy version.</summary>
    public string Id => "legacy-print-v1";

    /// <summary>Gets the structural limits (element count, DOM depth, HTML bytes) enforced by this policy.</summary>
    public PdfPolicyLimits Limits => PdfPolicyLimits.Strict;

    /// <summary>
    /// Validates <paramref name="context"/> against the legacy-print CSS policy rules.
    /// Checks structural limits and, when the context is an <see cref="AngleSharpStyledDocument"/>,
    /// walks the stylesheet AST and computed element styles for forbidden CSS features.
    /// In soft-degrade mode, <c>display:flex/grid</c> violations are emitted as
    /// <see cref="PolicySeverity.Warning"/> and rendering proceeds; all other forbidden
    /// features remain hard errors.
    /// </summary>
    /// <param name="context">The styled document context to validate.</param>
    /// <param name="ct">Cancellation token (unused; reserved for interface conformance).</param>
    /// <returns>
    /// <see cref="PolicyValidationResult.Ok"/> when no violations are found; a passing
    /// <see cref="PolicyValidationResult"/> (accepted = <c>true</c>) when only warnings are
    /// present in soft-degrade mode; otherwise a failed result listing all violations.
    /// </returns>
    public ValueTask<PolicyValidationResult> ValidateAsync(
        IPdfDocumentContext context,
        CancellationToken ct = default)
    {
        var violations = new List<PolicyViolation>();

        CheckLimits(context, violations);

        if (context is AngleSharpStyledDocument styledDoc)
            CheckCssFeatures(styledDoc.AngleSharpDocument, violations, _softDegrade);

        // Accepted = true when there are no violations at all, OR when soft-degrade is on and
        // every violation is a Warning (no hard errors). Any Error-severity violation rejects.
        bool accepted = violations.All(v => v.Severity == PolicySeverity.Warning);
        return ValueTask.FromResult(accepted && violations.Count == 0
            ? PolicyValidationResult.Ok
            : new PolicyValidationResult(accepted, violations));
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

    private static void CheckCssFeatures(IDocument document, List<PolicyViolation> violations, bool softDegrade)
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

        // Soft-degrade: track whether any flex/grid sub-properties were encountered per page
        // so we can emit at most one aggregate warning each (not one per property per element).
        bool flexSubPropSeen = false;
        bool gridSubPropSeen = false;

        // Soft-degrade telemetry: track whether any flex or grid display was downgraded.
        bool softDegradeFlexTriggered = false;
        bool softDegradeGridTriggered = false;

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
            {
                if (softDegrade)
                {
                    violations.Add(SoftDegradeViolationFor("soft-degrade.display.flex", "display", display, selector));
                    softDegradeFlexTriggered = true;
                }
                else
                    violations.Add(ViolationFor("forbidden.display.flex", "display", display, selector, "display:block"));
            }

            if (display is "grid" or "inline-grid")
            {
                if (softDegrade)
                {
                    violations.Add(SoftDegradeViolationFor("soft-degrade.display.grid", "display", display, selector));
                    softDegradeGridTriggered = true;
                }
                else
                    violations.Add(ViolationFor("forbidden.display.grid", "display", display, selector, "display:table"));
            }

            // position:absolute is ALLOWED in Profile v1 (CSS 2.1 print layout).
            // position:fixed and position:sticky remain blocked.
            if (position is "fixed")
                violations.Add(ViolationFor("forbidden.position.fixed", "position", position, selector, "position:static"));
            if (position is "sticky")
                violations.Add(ViolationFor("forbidden.position.sticky", "position", position, selector, "position:static"));

            // Soft-degrade: flex/grid sub-properties are silently dropped.
            // Emit at most one aggregate warning per kind (flex vs grid) per page.
            if (softDegrade)
            {
                for (int pi = 0; pi < style.Length; pi++)
                {
                    string propName = style[pi];
                    if (!FlexGridSubProperties.Contains(propName)) continue;

                    bool isGrid = propName.StartsWith("grid", StringComparison.OrdinalIgnoreCase);
                    if (isGrid)
                    {
                        if (!gridSubPropSeen)
                        {
                            gridSubPropSeen = true;
                            violations.Add(new PolicyViolation(
                                "soft-degrade.grid-subproperty",
                                "Grid sub-properties (grid-template-*, grid-column*, grid-row*, etc.) are not supported and will be ignored. Element will render as block.",
                                Severity: PolicySeverity.Warning,
                                PropertyName: propName,
                                CssSelector: selector,
                                SuggestedAlternative: "Remove grid sub-properties or set PdfConfigs:Policy:SoftDegradeUnknownDisplay=false to fail-loud"));
                        }
                    }
                    else
                    {
                        if (!flexSubPropSeen)
                        {
                            flexSubPropSeen = true;
                            violations.Add(new PolicyViolation(
                                "soft-degrade.flex-subproperty",
                                "Flex sub-properties (flex-grow, flex-shrink, justify-content, align-items, gap, etc.) are not supported and will be ignored. Element will render as block.",
                                Severity: PolicySeverity.Warning,
                                PropertyName: propName,
                                CssSelector: selector,
                                SuggestedAlternative: "Remove flex sub-properties or set PdfConfigs:Policy:SoftDegradeUnknownDisplay=false to fail-loud"));
                        }
                    }
                }
            }

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

        // Soft-degrade telemetry: emit counter once per page per kind.
        if (softDegradeFlexTriggered)
            _softDegradeCounter.Value.Add(1, new TagList { { "kind", "flex" } });
        if (softDegradeGridTriggered)
            _softDegradeCounter.Value.Add(1, new TagList { { "kind", "grid" } });

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

    private static PolicyViolation SoftDegradeViolationFor(
        string ruleId, string property, string value, string selector) =>
        new(ruleId,
            $"CSS property '{property}' with value '{value}' is not supported by the engine. Element will be rendered as display:block (soft-degrade mode).",
            Severity: PolicySeverity.Warning,
            PropertyName: property,
            RejectedValue: value,
            CssSelector: selector,
            SuggestedAlternative: "Remove display:flex/grid or set PdfConfigs:Policy:SoftDegradeUnknownDisplay=false to fail-loud");

    private static bool IsExternalUri(string? href) =>
        href is not null &&
        (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         href.StartsWith("//", StringComparison.Ordinal));
}
