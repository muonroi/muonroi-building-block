---
phase: 12-owned-css-cascade-b1
reviewed: 2026-06-19T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs
  - src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs
  - src/Muonroi.Pdf.Governance/Cascade/OwnedComputedStyle.cs
  - src/Muonroi.Pdf.Governance/Cascade/OwnedStyledNode.cs
  - src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs
findings:
  critical: 1
  warning: 6
  info: 4
  total: 11
status: issues_found
---

# Phase 12: Code Review Report

**Reviewed:** 2026-06-19
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Reviewed the owned CSS computed-style cascade (B1) that replaces AngleSharp.Css `GetComputedStyle`: a 7-step resolver (`CascadeResolver`), a document rule index with a supplemental raw-CSS recovery parser (`CssRuleSet`), the `IComputedStyle`/`IStyledNode` adapters, and the document wrapper.

The cascade structure is sound and the no-silent-catch convention is honored in `CascadeResolver.ApplyAuthorRules`. However the review surfaced one BLOCKER (shared-list aliasing in the supplemental map that corrupts declarations across selectors that share a parsed block) and several correctness WARNINGs in the cascade algorithm: `em` font-size resolution ignores the inherited parent font-size, specificity packing can collide on dense selectors, `!important` is matched without a separating-space requirement, and `text-decoration` / `font-style` UA defaults are written before inheritance can apply in the wrong precedence. These can produce wrong computed styles even though the current golden suite (534/0) passes — the goldens do not exercise the triggering inputs.

## Critical Issues

### CR-01: Shared mutable list aliased across selectors corrupts supplemental declarations

**File:** `src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs:206-225`
**Issue:** In `ParseRawCssForSupplemental`, a single parsed block produces one `supplementalDecls` list, and that *same list reference* is stored under every simple selector of a grouped rule:

```csharp
string[] simpleSelectors = SplitGroupedSelector(selectorText);
foreach (string simple in simpleSelectors)
{
    string key = simple.Trim();
    if (!map.TryGetValue(key, out var existing))
        map[key] = supplementalDecls;   // <-- SAME reference stored for .a and .b
    else
        // mutates `existing` in place
        foreach (var decl in supplementalDecls)
            if (!existingProps.Contains(decl.Property))
                existing.Add(decl);
}
```

For input such as:
```css
.a, .b { word-break: break-word }
.a     { white-space: nowrap }
```
the first rule stores the *identical* list object under both `.a` and `.b`. The second rule hits the `existing` branch for `.a` and calls `existing.Add(white-space)` — but because `.a` and `.b` alias the same list, `.b` now also gets `white-space: nowrap`, which it was never authored to have. The supplemental block then injects a wrong property into `.b`'s cascade in `FromDocument` (line 71-80), producing an incorrect computed style. This is a data-correctness defect, not a style nit.

**Fix:** Store a defensive copy per key so each selector owns its own list:
```csharp
foreach (string simple in simpleSelectors)
{
    string key = simple.Trim();
    if (!map.TryGetValue(key, out var existing))
        map[key] = new List<CssDeclaration>(supplementalDecls); // copy, not alias
    else
    {
        var existingProps = new HashSet<string>(
            existing.Select(d => d.Property), StringComparer.OrdinalIgnoreCase);
        foreach (var decl in supplementalDecls)
            if (!existingProps.Contains(decl.Property))
                existing.Add(decl);
    }
}
```

## Warnings

### WR-01: `em` resolution ignores inherited parent font-size

**File:** `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs:706-714`
**Issue:** `ResolveUnits` computes `fontSizePx` only from *this element's own* `font-size` entry, defaulting to `RootFontSizePx` (16px) when absent. But by CSS, `em` resolves against the element's *computed* font-size, which is inherited from the parent when the element sets no font-size of its own. Inheritance (Step 6) does copy `font-size` into `map` before `ResolveUnits` runs (Step 7), so an inherited `font-size` is present — *unless* the inherited value is itself a non-px literal (e.g. `%`, or an `em` that the parent left unresolved). For an element under a parent with `font-size: 0.5em → 8px`, the child correctly inherits `8px`; but for `padding: 2em` on an element whose font-size was authored as `120%` and never resolved to px, `ParseLengthToPx("120%")` returns 0 (line 775-776), so `fontSizePx` silently falls back to 16px and the `em` is resolved against the wrong base. Result: incorrect px output for `em` lengths on percentage-font-size subtrees.

**Fix:** Resolve the effective font-size to px up front (inheriting the parent's *resolved px* font-size when the local value is a `%` or otherwise unresolvable), and thread the parent's resolved font-size into `ResolveUnits`. At minimum, when `ParseLengthToPx` of the local font-size yields 0 and a parent value exists, use the parent's resolved px font-size rather than `RootFontSizePx`.

### WR-02: Specificity packing collides for dense selectors (≥100 classes/tags)

**File:** `src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs:330,399`; `CascadeResolver.cs:260`
**Issue:** Specificity is packed as `ids*10000 + classes*100 + tags`. A selector with ≥100 tag components (`tags >= 100`) overflows into the classes column, and ≥100 classes overflows into the ids column, so a high-tag-count selector can falsely outrank a single-class selector. CSS specificity components are not base-100 bounded. While 100+ compound parts is rare in legacy print HTML, the packing also *silently* mis-orders rather than failing, so any miscount in `ComputeSpecificityFromText` (which approximates, not parses) compounds the risk.

**Fix:** Use a tuple/struct comparison `(ids, classes, tags)` ordered lexicographically instead of a packed int, or widen the radix (e.g. `*1_000_000` / `*1_000`) to a value no real selector reaches. A `(int,int,int)` comparator in `Compare` is the clean fix and removes the magic `10000`/`100`.

### WR-03: `!important` matched without requiring a separating boundary

**File:** `src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs:243-244`; `CascadeResolver.cs:289-291`
**Issue:** Both the supplemental parser and inline-style parser detect importance via `value.EndsWith("!important", OrdinalIgnoreCase)` and strip exactly `"!important".Length` chars. A value that legitimately *ends* with the literal text `!important` as part of a token (e.g. a custom font family name or a value like `foo!important` with no space) would be misclassified and truncated. More practically, the CSS grammar permits whitespace between value and bang (`break-word ! important`) which this does NOT detect — so an authored `!important` with internal spacing is silently treated as a non-important normal declaration, dropping its precedence. Inconsistent importance handling can flip the cascade winner.

**Fix:** Normalize by collapsing internal whitespace before the bang and require a whitespace boundary before `!important`, e.g. match a trailing `(\s|^)!\s*important$` (regex) or trim trailing whitespace then check `EndsWith("!important")` after also stripping an interior space variant. At minimum document that `! important` (spaced) is unsupported.

### WR-04: UA `text-decoration`/`font-style`/`font-weight` are not inheritance-aware and can mask author intent on ancestors

**File:** `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs:610-650`
**Issue:** `text-decoration` is NOT in `InheritedProperties` (correct per CSS — it propagates by box, not inheritance), but `font-style`/`font-weight` ARE inherited. The UA layer sets `font-weight: bold` on `b/strong/h1–h6/th` and `font-style: italic` on `i/em` only when unset. Because UA defaults (Step 5) run *before* inheritance (Step 6), and inheritance only fills *unset* properties, an `<em>` nested in an element whose author CSS set `font-style: normal` will get the UA `italic` (Step 5 fills it) and the parent's `normal` never applies — which is correct CSS. However, for `font-weight` the same ordering means a `<th>` inside a `font-weight: normal` table correctly stays bold. These are fine. The actual gap: `ApplyUaDefaults` keys off `element.LocalName` lowercased but `UaInlineTags`/`UaBlockTags` membership for `display` is filled only when `display` is unset — there is no UA default for unknown/custom elements, so a custom-tag element gets no `display` at all (empty), and downstream layout reading `display` via `GetValue` receives `null`. AngleSharp's `GetComputedStyle` returned `inline` for unknown elements. This is a behavioral regression for custom tags.

**Fix:** Add a final fallback in the `display` block: `else map["display"] = "inline";` (CSS default for unknown elements) so no element resolves to a null `display`.

### WR-05: `ExpandShorthands` border longhand precedence is order-dependent and drops author longhands set after the shorthand

**File:** `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs:312-370`
**Issue:** Shorthand expansion runs over the post-cascade `map`, where last-wins has already collapsed each property to a single value. If the author wrote `border: 1px solid red; border-top-width: 3px`, the cascade keeps both keys (`border` and `border-top-width`) since they are different property names; expansion then calls `SetIfAbsent` for `border-top-width`, which sees the author longhand already present and keeps `3px` — correct. But the reverse order `border-top-width: 3px; border: 1px solid red` is *also* kept as both keys with the same final values, so expansion again keeps `3px` and ignores the later `border` shorthand's top width — which is WRONG per CSS (a later shorthand should reset earlier longhands). The cascade collapsed away the source order needed to resolve shorthand-vs-longhand precedence, so expansion cannot reconstruct it.

**Fix:** Either expand shorthands *during* `ApplyAuthorRules` (before last-wins collapse, preserving source order per longhand), or record the source order of the shorthand vs each longhand and let the later one win. The current `SetIfAbsent`-after-collapse approach cannot be correct for shorthand-after-longhand authoring.

### WR-06: `ExpandFont` and `ExpandBackground` silently discard recognized sub-values; no diagnostic when a shorthand is only partially parsed

**File:** `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs:516-581`
**Issue:** `ExpandFont` stops contributing family parts unless a size token was found (`sizeFound`), and `font: inherit`/`font: caption` (system-font keywords) are silently ignored entirely — no `font-size`/`font-family` is set and the shorthand key was already removed (line 360), so the declaration vanishes with no trace. `ExpandBackground` likewise drops everything that is not a bare color. Per the project's no-silent-catch / evidence-first posture, silently discarding an authored declaration with no debug log makes mis-rendering undiagnosable. (`CascadeResolver` already holds an `ILogger? _logger`, but `ExpandShorthands` is `static` and has no access to it.)

**Fix:** Make the expansion methods instance methods (or pass the logger) and `_logger?.LogDebug` when a shorthand value is removed but produced zero longhands, including the property and raw value.

## Info

### IN-01: Step C is documented but unimplemented — dead comment

**File:** `src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs:103-107`
**Issue:** The "Step C" comment block describes adding raw-text-only rules that have no CSSOM anchor, then does nothing (the method returns immediately after). This is a misleading comment that implies behavior that does not exist.
**Fix:** Either implement the described fallback or delete the comment to avoid implying coverage that isn't there.

### IN-02: `ComputeTotalStylesheetBytes` double-counts / mismatches the supplemental walk source

**File:** `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs:70-79`
**Issue:** Stylesheet bytes are computed from `sheet.OwnerNode?.TextContent`, while `CssRuleSet.BuildSupplementalFromRawStyleText` walks `document.GetElementsByTagName("style")`. These two sources can diverge (linked/imported sheets have an owner node but no `<style>` text; `<style>` in `<body>` may or may not be a registered stylesheet). Not a correctness bug for the cascade, but the byte metric and the parsed set are inconsistent.
**Fix:** Document the intended source of truth, or unify both on `document.StyleSheets`.

### IN-03: Magic numbers for unit conversion lack named constants

**File:** `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs:783,794,797`
**Issue:** `4f/3f` (pt→px), `3.7795f` (mm→px), `37.795f` (cm→px) are inline magic numbers. `RootFontSizePx` is a named const but the conversion factors are not, and the mm/cm factors are rounded approximations that will not round-trip exactly against the `*4/3` pt value at the same DPI.
**Fix:** Hoist to named constants (`PxPerPt`, `PxPerMm`, `PxPerCm = PxPerMm * 10`) and derive cm from mm to keep them consistent.

### IN-04: `OwnedStyledNode.Children` rebuilds children list and re-allocates wrapper nodes on every access

**File:** `src/Muonroi.Pdf.Governance/Cascade/OwnedStyledNode.cs:101-119`
**Issue:** `Children` is a property with no caching — each access re-walks `_node.ChildNodes` and allocates fresh `OwnedStyledNode` instances, each of which will re-run the resolver on its first `Style` access. The class caches `_cachedStyle`/`_resolvedMap` per node but not the children list, so repeated `Children` access re-resolves the entire subtree. (Performance is out of v1 scope, but this is also a *correctness*-adjacent surprise: two `Children` calls return non-identical node objects with independent caches, breaking any identity assumption a consumer might make.)
**Fix:** Cache the constructed children list in a backing field and return it on subsequent access.

---

_Reviewed: 2026-06-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
