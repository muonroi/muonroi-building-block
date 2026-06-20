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
            // AngleSharp.Css (beta) throws on values it cannot compute headlessly — ArgumentException
            // for em/rem/% (Phase 12) and NullReferenceException for some transform functions
            // (e.g. translate()). Both degrade to the raw author stylesheet rules below.
            catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
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

            if (style is null)
            {
                // GetComputedStyle failed AND no stylesheet rule matched. Inline style="" values are
                // invisible to the rule scan, so check the raw inline attribute directly for the
                // Phase 14 properties (transform / gradient) — otherwise an inline radial-gradient or
                // translate() would silently bypass the gate.
                string inlineCss = element.GetAttribute("style") ?? string.Empty;
                if (inlineCss.Length > 0)
                {
                    string inlineSel = element.GetSelector() ?? element.LocalName;
                    CheckTransformAndGradient(
                        InlineDeclValue(inlineCss, "transform"),
                        InlineDeclValue(inlineCss, "background"),
                        InlineDeclValue(inlineCss, "background-image"),
                        inlineSel, violations);
                }
                continue;
            }

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

            // Phase 14: a single transform:rotate(<angle>) is supported (rendered via a rotation CTM,
            // e.g. a diagonal watermark) and linear-gradient backgrounds. All other transforms and
            // gradient functions remain rejected. See CheckTransformAndGradient.
            CheckTransformAndGradient(
                style.GetPropertyValue("transform"),
                style.GetPropertyValue("background"),
                style.GetPropertyValue("background-image"),
                selector, violations);
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

    // Phase 15: allowed affine transform function names (D-02). Any function not in this set is
    // rejected fail-loud via the IsAffineTransform gate called in CheckTransformAndGradient.
    private static readonly System.Collections.Generic.HashSet<string> AllowedAffineFunctions =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
            "translate", "translateX", "translateY",
            "scale", "scaleX", "scaleY",
            "rotate",
            "skew", "skewX", "skewY",
            "matrix"
        };

    // Phase 15: tokenizes a CSS transform string into name(args) pairs.
    // Non-backtracking-prone: no nested quantifiers on the same group (T-15.01-02).
    private static readonly System.Text.RegularExpressions.Regex AffineFunctionTokenRegex = new(
        @"(\w+)\(([^)]*)\)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
        | System.Text.RegularExpressions.RegexOptions.CultureInvariant
        | System.Text.RegularExpressions.RegexOptions.Compiled);

    // Phase 15: returns true if every function token in the transform value is an allowed affine
    // function with numerically-parseable args. Rejects any unknown function name fail-loud (D-02).
    // Empty/null input returns false (no transform — treated as no violation by caller).
    private static bool IsAffineTransform(string transform)
    {
        if (string.IsNullOrWhiteSpace(transform)) return false;
        var matches = AffineFunctionTokenRegex.Matches(transform);
        if (matches.Count == 0) return false;
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (!AllowedAffineFunctions.Contains(m.Groups[1].Value)) return false;
            if (!AreNumericArgs(m.Groups[2].Value)) return false;
        }
        return true;
    }

    // Verifies that a CSS function's args string is composed only of comma-separated numbers
    // with optional CSS angle/length units. Accepts empty args (e.g. no-arg edge cases).
    private static bool AreNumericArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return true;
        foreach (string part in args.Split(','))
        {
            string raw = part.Trim();
            if (raw.Length == 0) continue;
            // Strip trailing CSS unit (deg/rad/grad/turn/px/%)
            foreach (string unit in new[] { "deg", "grad", "turn", "rad", "px", "%" })
            {
                if (raw.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                {
                    raw = raw[..^unit.Length].TrimEnd();
                    break;
                }
            }
            if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                return false;
        }
        return true;
    }

    // Phase 15: shared transform + gradient gate (computed-style and inline-style paths both call it).
    private static void CheckTransformAndGradient(
        string? transform, string? background, string? backgroundImage,
        string selector, List<PolicyViolation> violations)
    {
        transform ??= string.Empty;
        background ??= string.Empty;
        backgroundImage ??= string.Empty;

        if (!string.IsNullOrEmpty(transform) && !IsAffineTransform(transform))
        {
            violations.Add(ViolationFor("forbidden.transform.geometric", "transform", transform, selector,
                "Only affine transform functions (translate, scale, rotate, skew, matrix) are supported; " +
                "perspective/filter and unknown functions are rejected."));
        }

        string gradientSource = background.Contains("gradient", StringComparison.OrdinalIgnoreCase)
            ? background
            : backgroundImage;
        if (gradientSource.Contains("gradient", StringComparison.OrdinalIgnoreCase))
        {
            bool isAllowedGradient =
                (gradientSource.Contains("linear-gradient(", StringComparison.OrdinalIgnoreCase)
                 || gradientSource.Contains("radial-gradient(", StringComparison.OrdinalIgnoreCase))
                && !gradientSource.Contains("conic-gradient", StringComparison.OrdinalIgnoreCase)
                && !gradientSource.Contains("repeating-", StringComparison.OrdinalIgnoreCase);
            if (!isAllowedGradient)
            {
                violations.Add(ViolationFor("forbidden.background.gradient", "background",
                    gradientSource, selector,
                    "Use linear-gradient or radial-gradient; other gradient functions are not supported."));
            }
        }
    }

    // Extracts a single declaration value from a raw inline style="" attribute (no CSSOM).
    private static string InlineDeclValue(string css, string property)
    {
        foreach (string decl in css.Split(';'))
        {
            int colon = decl.IndexOf(':');
            if (colon <= 0) continue;
            if (decl.AsSpan(0, colon).Trim().Equals(property, StringComparison.OrdinalIgnoreCase))
                return decl[(colon + 1)..].Trim();
        }
        return string.Empty;
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
