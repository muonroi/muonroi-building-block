# Phase 12: Owned CSS Cascade (B1) - Context

**Gathered:** 2026-06-19
**Status:** Ready for planning
**Source:** Design doc + spike (this session)

<domain>
## Phase Boundary

Replace `AngleSharp.Css.GetComputedStyle` (beta.147, throws on em/rem/% headless — root cause of the
G14–G29 per-property fallback class) with an **owned computed-style cascade**. AngleSharp.Css is
demoted to a **parser** (rules + `@page` + `@font-face`); AngleSharp **core** handles selector
matching via `element.Matches`. Abstractions (`IComputedStyle`/`IStyledNode`/`IStyledDocument`) do
NOT change — only the Governance implementation behind the seam changes; BoxTreeBuilder + layout
engine are untouched at the seam.

**This plan = B1.1 only.** B1.2 (policy migration) and B1.3 (delete fallbacks) are separate.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### Direction
- Option **B1** chosen. Option A (render device) is **dead** — spike proved beta.147 ignores the
  device in all 4 wirings; throw persists; output incomplete (`word-break=''`). See spike doc.
- Option B2 (drop AngleSharp.Css entirely) rejected: parsing was never the problem; B1 keeps the
  reliable parsing and owns only the broken computed-style resolution.

### Components (in `src/Muonroi.Pdf.Governance/Cascade/`)
- `CssRuleSet` — collect rules once from `document.StyleSheets` (`ICssStyleRule.SelectorText` +
  declarations via `ICssStyleRule.Style`, incl. `GetPropertyPriority=="important"`); split grouped
  selectors; specificity (prefer `ISelector.Specificity`); source order.
- `CascadeResolver` — per element: `element.Matches(selectorText)` (core) → sort by
  (important, specificity, order) → overlay inline `style=""` → expand profile shorthands
  (border/border-side/margin/padding/background/font/text-decoration) → UA defaults layer
  (table display map, `th` bold+center, `h1-h6` bold, b/strong/i/em/u, hr) → inheritance for
  inherited props → unit resolution (em/rem→px via font-size chain; `%` left literal; px/pt as-is).
- `OwnedComputedStyle : IComputedStyle` — wraps resolved map; complete for the profile surface.
- `OwnedStyledNode : IStyledNode` — lazily resolves `Style` via resolver, threading parent's
  resolved map for inheritance; caches per node. Replaces `AngleSharpStyledNode` internals.

### Constraints
- **Never** call `IWindow.GetComputedStyle` / `ComputeCurrentStyle`. Remove the
  `catch (ArgumentException)` inline-only path in `AngleSharpStyledNode`.
- Keep BoxTreeBuilder fallbacks (`LookupClassProperty` / `LookupDescendantClassProperty` /
  `ParseInlineStyleProperty` / per-property `if computed==0`) as belt-and-suspenders in B1.1 — they
  simply never fire once computed styles are complete. Deleted in B1.3.
- `@page` (`AngleSharpPageRule`) + `@font-face` (`ExtractFontFaces`) extraction stay as-is.
- Property surface bounded by Legacy Print Profile v1 (see PROFILE-V1.md / design §2).

### Verification
- Full `Muonroi.Pdf.Tests` suite green. Re-baseline ONLY `%`-table cases (TCIS HBCX corpus),
  visually verified. Simple-doc goldens must stay byte-identical. Determinism canary unaffected.
- The G25/G27/G28/G29 regression scenarios must pass via the cascade (repoint, don't delete).

### Claude's Discretion
- Internal class/file layout, specificity computation details if `ISelector.Specificity` is
  insufficient, the exact inherited-property allow-list (start from CSS 2.1 inherited set ∩ profile).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Design & spike
- `.planning/DESIGN-owned-cascade-B1.md` — full architecture, cascade algorithm, migration phases, risks, test strategy (authoritative).
- `.planning/SPIKE-cascade-render-device.md` — why Option A is dead (evidence).

### Current cascade seam (to replace)
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledNode.cs` — the `GetComputedStyle` + catch path.
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs` — builds root node; holds window.
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpComputedStyle.cs` — current `IComputedStyle` impl.
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpCascadeEngine.cs` — entry (`CascadeAsync`).
- `src/Muonroi.Pdf.Abstractions/Engine/IComputedStyle.cs` / `IStyledNode.cs` / `IStyledDocument.cs` — unchanged seam.

### Consumer (must keep working unchanged)
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — reads `IComputedStyle.GetValue`; holds the G14–G29 fallbacks (kept in B1.1).

### Profile surface
- `PROFILE-V1.md` (repo root) — the bounded CSS property set the cascade must compute.
</canonical_refs>

<specifics>
## Specific Ideas
- Reuse AngleSharp core `element.Matches(selectorText)` for selector matching (spike confirmed it
  resolves `.table-bodered2 td` correctly without the em/rem throw).
- The G25/G27/G28/G29 regression tests in `tests/.../DescendantClassSelectorAndThBoldTests.cs` are
  the cascade's acceptance bar for descendant selectors / shorthand / inheritance.
</specifics>

<deferred>
## Deferred Ideas
- **B1.2** — migrate `LegacyPrintPolicy` / `DefaultStrictPolicy` off `GetComputedStyle` to the cascade.
- **B1.3** — delete the G14–G29 BoxTreeBuilder fallbacks; repoint Gxx tests at the cascade.
- Option B2 (own CSS parser, drop AngleSharp.Css entirely) — revisit only if beta churn forces it.
</deferred>

---

*Phase: 12-owned-css-cascade-b1*
*Context captured: 2026-06-19*
