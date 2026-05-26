# Phase 3 Discussion Log: Box Tree + Layout Engine

**Date**: 2026-05-27
**Mode**: Headless autonomous — all decisions made by Claude without interactive input
**Phase**: 3 of 9

---

## Summary

Six gray areas were identified, reasoned through, and resolved autonomously.

---

## Gray Area 1: How does the layout engine access computed styles?

**Options presented**:
A. `InternalsVisibleTo("Muonroi.Pdf")` — let layout engine cast to `AngleSharpStyledDocument` across assemblies
B. Extend `IStyledDocument` with `IStyledNode` traversal interface in Abstractions
C. Move layout engine into `Muonroi.Pdf.Governance` (co-located with cascade)

**Selection**: B — Extend `IStyledDocument` with traversal interface

**Notes**: Option A creates tight assembly coupling and breaks the seam. Option C violates the ROADMAP's explicit placement of the engine in `Muonroi.Pdf`. Option B is the correct seam design — the layout engine has zero AngleSharp dependency, enabling clean v0.2 AOT and source generator work. The `IStyledNode`/`IComputedStyle`/`IPageRule` interfaces are additive to Abstractions (marker interface stays marker-compatible — existing implementations return `null` for new members if needed, though `AngleSharpStyledDocument` is the only implementation).

---

## Gray Area 2: Where does `@page` rule parsing live?

**Options presented**:
A. In layout engine — walk stylesheet AST in `Muonroi.Pdf`; requires Governance reference
B. In Governance cascade phase — extract at construction time, expose via `IStyledDocument.PageRule`

**Selection**: B — Extract in Governance, expose via `IPageRule` interface

**Notes**: Option A requires `Muonroi.Pdf` to reference `AngleSharp` directly, which the package should not do (AngleSharp is Governance's concern). Option B keeps parsing co-located with the cascade phase. The `IPageRule` extraction adds only ~20 lines to `AngleSharpStyledDocument` construction.

---

## Gray Area 3: Two-pass layout for `counter(pages)` — implement or defer?

**Options presented**:
A. Implement full two-pass in Phase 3
B. Defer to Phase 4 — ship with `counter(pages)` as static "?" placeholder
C. Single-pass with pre-estimated page count (fragile)

**Selection**: A — Full two-pass in Phase 3

**Notes**: STATE.md explicitly records this as a blocking architecture decision that cannot be retrofitted. Success criterion SC5 requires correct resolution. The two-pass approach is standard for paginated documents. Deferral risks invalidating the `IPositionedPageList` contract design that Phase 5 depends on. The performance cost (2× layout) is acceptable given the ≤300 ms target is measured at Phase 7 (full pipeline), not Phase 3 alone.

---

## Gray Area 4: Font metrics without Phase 4?

**Options presented**:
A. Block Phase 3 on Phase 4 (sequential dependency)
B. Hardcode Arial/approximate metrics
C. `ITextMetrics` internal seam with `EstimatedTextMetrics` impl

**Selection**: C — `ITextMetrics` seam, Phase 4 replaces the implementation

**Notes**: Blocking Phase 3 on Phase 4 serializes work unnecessarily — table column sizing and block layout don't require font metrics. The `EstimatedTextMetrics` uses `fontSize * 0.6` per char, acceptable for layout unit tests. Phase 4 ships `SixLaborsTextMetrics` and the layout engine requires no change — only the DI wiring in Phase 6 swaps the implementation.

---

## Gray Area 5: `IPositionedPageList` — keep as marker or add traversal?

**Options presented**:
A. Keep as marker interface; Phase 5 writer casts to `PositionedPageList` (same assembly)
B. Add traversal interface to Abstractions now

**Selection**: A — Stay marker; internal cast in Phase 5

**Notes**: Adding traversal to Abstractions now locks Phase 5 to a specific positioned-box model before the PDF writer design is known. The marker pattern worked cleanly in Phase 2 for `IStyledDocument` → `AngleSharpStyledDocument`. The same-assembly internal cast in Phase 5 (both layout and PDF writer live in `Muonroi.Pdf`) is the correct pattern. If Phase 5 needs a richer interface, that decision belongs in Phase 5 context.

---

## Gray Area 6: `Muonroi.Pdf.csproj` Governance reference — add now or Phase 6?

**Options presented**:
A. Add `Muonroi.Pdf.Governance` reference to `Muonroi.Pdf.csproj` now (so orchestrator can be in `Muonroi.Pdf`)
B. Defer Governance reference to Phase 6 (DI wiring is Phase 6 scope)

**Selection**: B — Defer to Phase 6

**Notes**: The layout engine only consumes `IStyledDocument`/`IStyledNode` from Abstractions. No Governance concrete types are needed in the layout engine. Adding the reference now would allow Phase 3 to accidentally couple to AngleSharp internals, defeating the seam design decided in Gray Area 1.

---

## Deferred Ideas

- CSS `@page { size: ... }` override of `PdfRenderOptions.PageSize` — explicitly deferred to post-Phase 3; `KNOWN-DEVIATIONS.md` entry planned
- Orphans/widows pagination — no success criterion requires it in Phase 3; `KNOWN-DEVIATIONS.md` entry planned
- Two-pass third iteration for multi-digit boundary shift — accepted as known limitation; `KNOWN-DEVIATIONS.md` entry planned
- `counter(page)` inside header/footer content — deferred; header/footer is rendered from static HTML; nested counter resolution is not in Phase 3 success criteria

---

## Claude's Discretion Items

- The `ITextMetrics` internal seam was added autonomously (not in REQUIREMENTS) to prevent a hard dependency between Phase 3 and Phase 4 timelines. It is internal to `Muonroi.Pdf` and creates no public contract change.
- `EstimatedTextMetrics` uses `fontSize * 0.6` per char — a monospace approximation. This is intentionally rough; Phase 4 replaces it. Tests that depend on exact pixel positions should be deferred to Phase 4.
- The unit type decision (working in CSS units → converted to PDF points at write time) is not in REQUIREMENTS but is essential for determinism. 1 mm = 2.834646 pt (CSS spec constant). This conversion lives in `Units.cs` and is used throughout the layout engine.
