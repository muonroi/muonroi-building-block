# Pitfalls Research

**Domain:** HTML/CSS-to-PDF rendering engine — pure-managed .NET, open-core commercial
**Researched:** 2026-05-26
**Confidence:** HIGH (domain-specific; draws from known failure modes in CSS layout engines, .NET library authoring, PDF generation, and open-core commercialization)

---

## Critical Pitfalls

### Pitfall 1: Box-Model Misimplementation Causes Cascading Layout Failures

**What goes wrong:**
The hand-written box tree produces visually plausible output for simple cases but silently miscomputes margin collapsing, BFC root promotion, or inline formatting context boundaries. Tests pass on golden snapshots written against the buggy layout, and the engine ships with systematic errors that only manifest in real-world templates (nested tables, mixed block/inline siblings, percentage heights in paged context).

**Why it happens:**
CSS 2.1 box model is deceptively deep. `margin collapsing` has 12+ edge cases in the spec (parent-child collapse, adjacent sibling collapse, self-collapsing blocks, BFC walls). Developers implement the happy path, write tests against their own implementation, and never discover that the spec says something different. Golden snapshots lock in the bugs.

**How to avoid:**
- Write golden tests **against the CSS 2.1 spec test suite outputs**, not against your own rendering. Import the W3C CSS 2.1 conformance test cases (freely available) as the authoritative baseline for declared modules.
- Implement BFC root detection first, before margin collapsing — every BFC root creates a containment wall. Missing a BFC root causes neighboring margins to collapse through a wall that should exist.
- Use a dedicated test fixture per collapsing scenario with ASCII art comment showing expected geometry.
- Never treat `display: inline-block` as a shortcut for "just render a block here" — it creates an atomic inline that participates in IFC, not BFC.

**Warning signs:**
- Tables that display correctly in isolation but shift when placed inside a padded `<div>`
- First-child margins bleeding through a containing block's padding
- Page breaks occurring at wrong positions (usually caused by wrong block height calculation)

**Phase to address:** v0.1 — must be caught before golden snapshot corpus is established; fixing after locks in buggy baselines.

---

### Pitfall 2: AngleSharp.Css Beta API Breaks Without Warning

**What goes wrong:**
AngleSharp.Css 1.0.0-beta.146 is pinned because it is the only viable CSS cascade engine for .NET. A beta dependency means the maintainer can ship breaking changes without a semver major bump. Pinning prevents accidental upgrades but also means security fixes and bug fixes in the cascade engine require manual evaluation of each new beta before updating. Worse: if AngleSharp itself (the HTML parser, a separate package) releases a version that is incompatible with beta.146, the project is stuck.

**Why it happens:**
The ecosystem gap is real — there is no stable, maintained CSS cascade engine in the .NET managed space. Accepting a beta pin is the only viable choice, but teams often treat "pinned" as "solved" rather than as "managed risk that needs monitoring."

**How to avoid:**
- The `ICssCascadeEngine` adapter seam is already in the design — this is the correct prevention. Implement it as a real interface from day 1, not a thin wrapper that leaks AngleSharp types.
- Add a `KNOWN-DEPENDENCIES.md` entry documenting the beta pin, the last evaluated version, and the upgrade runbook (what to test when bumping).
- Subscribe to AngleSharp.Css releases on GitHub to catch breaking changes before they become blockers.
- Write integration tests at the `ICssCascadeEngine` boundary (computed value assertions), not just at the final PDF output. This gives a fast signal when an AngleSharp.Css upgrade breaks cascade behavior.

**Warning signs:**
- AngleSharp main package releases a new major that AngleSharp.Css beta.146 depends on at an older version — causes `NU1608` or runtime type-load failures.
- Computed style properties return `null` or unexpected types after a transitive dependency update.

**Phase to address:** v0.1 — establish the adapter seam; v0.2 — before AOT/trim work, verify AngleSharp.Css is trim-safe or isolate it behind the adapter.

---

### Pitfall 3: Deterministic Output Is Lost Through Hidden Timestamps or Object IDs

**What goes wrong:**
The engine claims byte-for-byte deterministic output, but a developer adds a metadata field, uses `DateTime.UtcNow` for a creation date in PDF Info dictionary, or PdfSharpCore generates a random document ID — and the test suite never catches it because golden comparison uses file-hash equality that was established **after** the non-deterministic element was introduced.

**Why it happens:**
PDF writers almost universally write a creation timestamp and a random document ID by default. PdfSharpCore inherits this from the upstream PDFsharp codebase. Developers focus on content correctness and forget that the binary envelope also has non-deterministic fields.

**How to avoid:**
- In the `IPdfWriter` adapter implementation, explicitly set: document ID to a SHA-256 of the input HTML bytes, creation date to a fixed epoch (e.g., `1970-01-01T00:00:00Z`), and producer/creator strings to fixed values (include engine version, not build timestamp).
- Add a determinism canary test: render the same HTML twice in the same process and assert `bytes1.SequenceEqual(bytes2)`. Run this on every CI push.
- Extend the canary: render on two different machines (CI matrix: Windows + Linux) and compare hashes. Cross-platform non-determinism is a separate failure mode from within-machine non-determinism.

**Warning signs:**
- Golden snapshot hashes change between CI runs on the same commit.
- File sizes differ by exactly a few bytes between runs (timestamp field padding).

**Phase to address:** v0.1 — bake into `IPdfWriter` implementation before any golden corpus is committed.

---

### Pitfall 4: Resource Resolver Contract Drift Enables SSRF

**What goes wrong:**
The `IResourceResolver` is designed bytes-only — the engine never dereferences URIs. But a developer, under deadline pressure, adds a "convenience" default implementation that resolves `http://` URIs to unblock a demo. The convenience impl makes it into a PR, passes review because it's "just a default," and the security boundary is broken. Future callers depend on the convenience behavior without knowing the risk.

**Why it happens:**
The security model is clear in the design doc but not enforced at the type level. A `IResourceResolver` that returns `null` for unresolvable URIs is a silent failure, not a loud rejection. Teams under demo pressure reach for the easiest path.

**How to avoid:**
- The default `IResourceResolver` in `Muonroi.Pdf` must be `ThrowingResourceResolver` — it throws a `PdfSecurityException` with a message explaining that external resource resolution is disabled by design.
- Provide `EmbeddedResourceResolver` (resolves from assembly embedded resources) and `ByteArrayResourceResolver` (caller supplies a `Dictionary<string, byte[]>`) as the only safe built-ins.
- Add a static analysis check (Roslyn analyzer or CI script) that fails the build if any class implementing `IResourceResolver` contains the strings `HttpClient`, `WebClient`, `Uri`, `http://`, or `https://`.

**Warning signs:**
- A PR introduces a `DefaultResourceResolver` that calls `new HttpClient()`.
- An integration test starts making network calls (detectable by mocking the network layer at the OS level in CI).

**Phase to address:** v0.1 — `ThrowingResourceResolver` must be the default before first NuGet publish.

---

### Pitfall 5: Cross-Tenant Cache Poisoning from Caller-Supplied Keys

**What goes wrong:**
A caller calls `IMPdfService.RenderAsync(html, options)` and passes a `templateId` in `options`. An internal cache key is constructed as `$"{templateId}:{contentHash}"` without including `TenantId`. Tenant A's rendered template is served to Tenant B on the next request with the same `templateId`.

**Why it happens:**
Caching is added after the core rendering works, under performance pressure. The developer uses the obvious key — template identifier + content hash — without thinking through multi-tenancy. The bug is nearly impossible to reproduce in unit tests because they typically use a single tenant.

**How to avoid:**
- Cache key construction must be a private sealed class `PdfCacheKey(TenantId, ContentHash)` in the engine internals — never a string interpolation that the caller can influence.
- `ITenantContext` is resolved from the ambient DI scope, never from caller-supplied parameters.
- Add an explicit test: two tenants render the same HTML, assert the cache returns separate entries and that modifying one tenant's cache does not affect the other.

**Warning signs:**
- Cache key construction uses string interpolation with any parameter from `PdfRenderOptions`.
- A `templateId` string from the caller appears directly in a cache key.

**Phase to address:** v0.1 — enforce before any caching is wired; v1.0 Enterprise HotReload extends this to Redis, where the same invariant must hold.

---

### Pitfall 6: Vietnamese Diacritic Stacking Silently Falls Back to Replacement Characters

**What goes wrong:**
SixLabors.Fonts performs glyph lookup for a Vietnamese character (e.g., `ề` — U+1EC1 LATIN SMALL LETTER E WITH CIRCUMFLEX AND GRAVE). The font supplied via `IFontResolver` does not contain that glyph. SixLabors.Fonts falls back silently to a replacement glyph or `□`, no exception is thrown, the PDF renders with missing characters, and the golden test for Vietnamese text passes because the snapshot was captured with the same broken font.

**Why it happens:**
Font fallback behavior in text rendering is nearly always silent. The glyph pipeline is deep: Unicode normalization → glyph lookup → fallback chain → notdef. Each step can silently degrade without surfacing an error to the caller.

**How to avoid:**
- The `IFontResolver` implementation must be validated at startup: load each registered font and assert it can render the Vietnamese Unicode block (U+1E00–U+1EFF) — at least 200 representative code points. Fail startup if any required glyph is missing.
- The golden Vietnamese corpus (≥10 snapshots) must be built with fonts that are verified to cover the full Vietnamese range (Noto Serif, Source Serif, Be Vietnam Pro, etc.).
- Add a CI step that renders the Vietnamese reference string `"Tiếng Việt có dấu: àáâãèéêìíòóôõùúýăđơưạắặẹẽểịọộờụứ"` and visually inspects the output (pixel-level check against a known-good snapshot).

**Warning signs:**
- Rendered PDF byte-for-byte matches the golden but visual inspection shows `□` characters (golden was captured with broken font).
- `SixLabors.Fonts` glyph count for a font is lower than 200 for the Vietnamese Unicode range.

**Phase to address:** v0.1 — before Vietnamese golden snapshots are committed.

---

### Pitfall 7: SixLabors.ImageSharp License Threshold Breach

**What goes wrong:**
`SixLabors.ImageSharp` has a commercial license threshold: above a certain NuGet download count or production use threshold, commercial use requires a paid license. The project ships with ImageSharp as a transitive dependency (via SixLabors.Fonts or image decoding), crosses the threshold, and receives a license demand after v0.1 ships.

**Why it happens:**
The threshold is not enforced at compile time and is easy to miss during initial development. License audits feel like future-problem.

**How to avoid:**
- Perform the ImageSharp license audit at M+1 as specified in the constraints — not after v0.1 ships.
- The `IImageDecoder` abstraction seam exists precisely for this swap. Document the swap path (replace `ImageSharp` adapter with a BSD/MIT alternative like `StbImageSharp` or `SkiaSharp`-managed-only build) before it is needed.
- If ImageSharp is used, document the OSS threshold in `README.md` as a deployment prerequisite: "If your organization's NuGet download count exceeds X, a SixLabors commercial license is required."
- Prefer `StbImageSharp` (MIT, no threshold) for PNG/JPEG decoding if it meets quality requirements — it is pure managed and threshold-free.

**Warning signs:**
- v0.1 ships, gains OSS traction, and the download count rises. SixLabors sends a license request.
- A PR removes the `IImageDecoder` adapter and calls `Image.Load()` directly — collapses the swap path.

**Phase to address:** v0.1 M+1 audit checkpoint — must be resolved before first NuGet publish.

---

### Pitfall 8: PdfSharpCore Stale Maintenance Blocks Security Fixes

**What goes wrong:**
PdfSharpCore is a community fork of an older PDFsharp codebase. If it is abandoned or goes stale (no releases for 12+ months), a PDF-layer vulnerability discovered in the base codebase has no patch path. The project is stuck: upgrading to upstream PDFsharp 6.x is a significant divergence (the reason PdfSharpCore was chosen).

**Why it happens:**
Single-maintainer community forks have bus-factor issues. The project depends on PdfSharpCore precisely because PDFsharp 6.x diverged too far — but that same divergence means there is no easy upgrade path if PdfSharpCore goes dark.

**How to avoid:**
- The `IPdfWriter` adapter is the correct mitigation — it is already in the design. The adapter must be a real, documented extension point with a sample implementation, not a leaky wrapper.
- Monitor PdfSharpCore's GitHub for activity. If there is no commit activity for 6 months, escalate evaluation of alternatives (migrate `IPdfWriter` to PDFsharp 6.x, or to a lower-level PDF byte writer).
- Add a `DEPENDENCY-HEALTH.md` that tracks last-release dates for all dependencies and sets alert thresholds (3 months no release = yellow, 6 months = red).

**Warning signs:**
- PdfSharpCore has no release or commit activity for 6+ months.
- A CVE is filed against PDFsharp or PdfSharpCore with no patch available.

**Phase to address:** v0.1 — `IPdfWriter` adapter must be genuinely swappable before v0.1 ships; v0.2 — evaluate swap to PDFsharp 6.x or alternative if PdfSharpCore health is yellow.

---

### Pitfall 9: CSS Policy Enforcement Is Incomplete — Unsupported Properties Silently Pass

**What goes wrong:**
`IPdfCssPolicy.DefaultStrict` is supposed to reject unsupported CSS (flex, grid, float, absolute positioning). But the policy checks only the properties that developers remembered to add to the allowlist. A template uses `position: relative` (not in the declared scope but not in the explicit blocklist either), the policy allows it, the layout engine silently ignores the positioning and renders in normal flow, and the template author believes relative positioning works.

**Why it happens:**
CSS has hundreds of properties. An allowlist approach requires explicitly listing every allowed property — a denylist approach requires listing every denied property. Both are incomplete if maintained manually. The gap between "what the engine implements" and "what the policy allows" creates a class of properties that are neither implemented nor rejected.

**How to avoid:**
- Implement the policy as a **strict allowlist**: only properties in the declared CSS subset are allowed; everything else is rejected with a structured diagnostic (`PdfPolicyViolation` with property name, value, and suggested alternative).
- The allowlist is generated from the same source of truth that documents the declared CSS subset — never maintained separately.
- Return `PdfRenderResult` with a `Warnings` collection for policy violations in permissive mode, and throw `PdfPolicyException` in strict mode. Default to strict.

**Warning signs:**
- A template renders "correctly" but the author is using a CSS property not in the declared subset.
- The `KNOWN-DEVIATIONS.md` grows faster than the policy allowlist.

**Phase to address:** v0.1 — policy implementation must cover the full CSS 2.1 property set before any golden tests are written.

---

### Pitfall 10: Bus Factor Kills v0.2 Source Generator Delivery

**What goes wrong:**
1 FTE carries the project through M+6. If that FTE is unavailable (illness, departure) for more than 2 weeks during the v0.2 development window (M+6–M+8), the source generator work stalls. Source generators are significantly more complex than runtime path work — they require deep Roslyn knowledge. A generalist replacement cannot ramp up quickly enough to maintain velocity.

**Why it happens:**
The project plan acknowledges bus factor but defers the second FTE hire to M+7. If the hire is delayed by 4–6 weeks (common in engineering hiring), the 2 FTE window shrinks to the point where v1.0 Enterprise delivery is at risk.

**How to avoid:**
- Begin the second FTE search at M+3, not M+6. Hiring takes 8–12 weeks in engineering.
- Document the source generator design and Roslyn incremental generator patterns before M+6, so a new hire can ramp up on v0.2 in parallel with v1.0 planning.
- Do not gate the second FTE hire on v0.1 GA — the hire should be **onboarding** at M+5, not starting interviews.

**Warning signs:**
- M+5 (v0.1 GA) passes with no second FTE hired or in final stages.
- Source generator spike has not been prototyped by M+4 (leaves insufficient runway to discover scope).

**Phase to address:** Project planning — not a code issue; address in hiring timeline.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip `ICssCascadeEngine` adapter, use AngleSharp.Css directly | Faster initial impl | Locked into beta dependency forever; swap path nonexistent | Never |
| Use `byte[]` instead of `Stream` for PDF output | Simpler API surface | LOH pressure on multi-page docs; non-streaming response pipeline | Never for public API; acceptable in internal tests |
| Hardcode `TenantId = "default"` in single-tenant mode | Simplifies early testing | Cache key structure changes when multi-tenancy is needed; breaking API change | Only in internal benchmarks, never in shipped code |
| Copy-paste PdfSharpCore internal types to work around missing API | Unblocks a deadline | Forks the dependency; upgrade path severed | Never — contribute upstream or add to `IPdfWriter` contract |
| Skip determinism canary test to save 10s in CI | Slightly faster CI | Byte-for-byte guarantee is unmeasured; violation ships silently | Never |
| Use `string.Format` cache keys with caller-supplied templateId | Obvious, easy | Cross-tenant cache poisoning (see Pitfall 5) | Never |
| Accept FluentAssertions v8+ to get a new assertion API | Better test DX | Commercial license required — violates repo-wide decision | Never without explicit written decision |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| AngleSharp.Css computed styles | Reading `IStyleDeclaration` values as strings and parsing manually | Use typed `ICssValue` accessors; strings lose unit information and require re-parsing |
| SixLabors.Fonts glyph metrics | Trusting `Advance` without accounting for kerning pairs | Always apply `FontMetrics` kerning lookup when measuring line widths for IFC |
| PdfSharpCore coordinate system | Assuming Y=0 is top-left | PdfSharp uses bottom-left origin; invert Y for all layout→PDF coordinate mapping |
| `@font-face` subsetting | Embedding the full font binary | Use `SixLabors.Fonts` subsetting API to extract only referenced glyph IDs; full fonts can be 10 MB+ |
| `counter(pages)` implementation | Computing total pages in a first pass | Two-pass rendering required: first pass counts pages, second pass substitutes the counter — design the layout pipeline for two passes from the start |
| Redis HotReload (v1.0) | Broadcasting invalidation to all tenants on any change | Invalidation messages must be scoped to `(TenantId, TemplateId)` — global invalidation causes unnecessary cold renders across all tenants |
| Enterprise Registry Postgres | Using `text` columns for policy config JSON | Use `jsonb` with a GIN index on policy keys for efficient per-tenant policy queries |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| DOM traversal on every layout pass | Warm render degrades from 80ms to 800ms as template complexity grows | Build the box tree once, traverse it for layout; never re-parse HTML per layout pass | Templates with >500 elements |
| Full font embedding without subsetting | PDF binary size grows to 8–15 MB for a single page with 2 fonts | Subset to referenced glyph IDs using SixLabors.Fonts subsetting API | Any font with >1000 glyphs (common for Unicode fonts) |
| `byte[]` accumulation for image blobs | GC Gen2 pressure, LOH fragmentation | Use `IMemoryOwner<byte>` pooled buffers from `MemoryPool<byte>.Shared`; never allocate raw `byte[]` for image data | Multiple images per document |
| Synchronous `Stream` reads in `IResourceResolver` | Threadpool starvation under concurrent renders | All `IResourceResolver` implementations must be async-native; `ValueTask<ReadOnlyMemory<byte>>` not `byte[]` | >10 concurrent render requests |
| Re-computing CSS cascade per element | Layout time is O(n²) in element count | AngleSharp.Css computes cascade once per style sheet + element combination; cache the computed style object keyed on element identity | Documents with >1000 elements and deep CSS specificity chains |
| Tenant cache with no eviction policy | Memory grows unbounded on a long-running service | Cache entries must have a TTL (default 15 minutes) and a max-size limit (default 200 entries) enforced by `IMemoryCache` options | After 24h uptime with >50 distinct templates per tenant |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| `IResourceResolver` that resolves `file://` URIs | SSRF: renders PDFs containing contents of `/etc/passwd` or Windows system files | `ThrowingResourceResolver` default; `file://` URI scheme explicitly blocked in URI parser before `IResourceResolver` is even called |
| `<script>` tags passed through to PDF as `/JavaScript` PDF action | Remote code execution risk in PDF readers that execute JS | `IHtmlParser` adapter must strip all `<script>` elements before the DOM is handed to layout; `IPdfWriter` must reject `/JavaScript` at write time (defense in depth) |
| Unlimited `MaxHtmlBytes` / `MaxDomDepth` | ZIP-bomb HTML causes OOM; deeply nested DOM causes stack overflow in recursive layout | `PdfConfigs.Limits` enforced in `IHtmlParser.ParseAsync` before DOM construction; checked before layout pass begins |
| `@font-face src: url(http://...)` with a network resolver | Exfiltration: attacker-supplied template fetches from attacker-controlled URL on every render | `IFontResolver` must be bytes-only; URL schemes in `@font-face src` are parsed and rejected unless scheme is `data:` |
| Signed policy config bypass via environment override | Attacker who controls environment can override policy without breaking the signature | `PolicyVerifier` must verify signature on every startup, not once at install; `MUONROI_PDF_POLICY_OVERRIDE=` env var must not exist |
| Cache key collision attack | Attacker crafts HTML that produces the same content hash as a target tenant's cached template, polluting the cache | Content hash must include `TenantId` + a server-side HMAC, not just SHA-256 of HTML bytes |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| `PdfPolicyViolation` returned only in `Warnings` | Developer uses unsupported CSS, gets a PDF with wrong rendering, never sees the warning | Default to throwing `PdfPolicyException` in strict mode; warn-mode must be explicitly opted-in with `PdfRenderOptions.PolicyMode = Permissive` |
| Cryptic `NullReferenceException` when `IFontResolver` returns null | Developer wastes hours tracing a font loading issue | `IFontResolver` contract must document: returning `null` for a requested font throws `PdfFontNotFoundException` with the font family name; never NPE |
| `AddPdf()` succeeds but first render fails at runtime | Developer thinks registration is correct but discovers the problem on first user request | Add a startup health check (`IHealthCheck` via `AddPdfHealthChecks()`) that renders a 1-line test document and verifies output byte count |
| No structured error for render timeout | `OperationCanceledException` bubbles with no context | Catch timeout cancellation in the render pipeline and throw `PdfRenderTimeoutException(elapsed, limit)` with actionable message |
| Page count metadata wrong until 2nd render | Developer adds `counter(pages)` and is confused why it shows `0` on first render | Document the two-pass requirement explicitly in XML docs on `PdfRenderOptions`; `counter(pages)` requires `TwoPass = true` (or is automatic when the counter is detected) |

---

## "Looks Done But Isn't" Checklist

- [ ] **Deterministic output:** Render the same HTML 3 times in the same process — assert byte equality across all 3. Also compare hashes across Windows and Linux CI agents.
- [ ] **Font subsetting:** Check rendered PDF binary size — a single-page doc with an embedded font should not exceed 500 KB. If it does, subsetting is not working.
- [ ] **Policy enforcement:** Try rendering a template with `display: flex` — assert that `PdfPolicyException` is thrown, not a silently wrong layout.
- [ ] **SSRF prevention:** Try `<img src="file:///etc/passwd">` — assert `PdfSecurityException`, not a file read error or empty image.
- [ ] **Cross-tenant isolation:** Render tenant A's template, then render the same HTML as tenant B — assert separate cache entries.
- [ ] **Vietnamese coverage:** Render the reference Vietnamese diacritic string and visually inspect (not just byte-compare) the output.
- [ ] **`counter(pages)` accuracy:** Render a 5-page document with `counter(pages)` in the footer — assert footer shows `5` on every page.
- [ ] **Timeout enforcement:** Submit a pathological HTML (10,000 deeply nested `<div>`) — assert `PdfRenderTimeoutException` within `MaxRenderDuration + 1s`.
- [ ] **Stream output:** Render a 50-page document — assert no byte array larger than the page size appears in Gen2 GC after render.
- [ ] **Assembly hash:** Run `InjectAssemblyHash.ps1` — assert the Enterprise stub package fails to load if the hash is modified.

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Buggy golden snapshots committed before layout correctness was verified | HIGH | Delete the golden corpus, fix layout engine, regenerate from W3C test suite reference outputs |
| AngleSharp.Css breaking beta bump breaks cascade | MEDIUM | Roll back the AngleSharp.Css pin in `Directory.Packages.props`; evaluate the delta before upgrading |
| SSRF IResourceResolver ships in a release | HIGH | Yank the NuGet package immediately; publish patched version with `ThrowingResourceResolver` default; issue security advisory |
| Cross-tenant cache poisoning discovered in production | CRITICAL | Flush all cache entries immediately (add cache flush admin endpoint); patch `PdfCacheKey` to include `TenantId`; audit all cache entries in Redis for affected tenants |
| SixLabors license threshold hit | MEDIUM | Swap `IImageDecoder` implementation to `StbImageSharp` (pre-built adapter); redeploy; retroactively purchase SixLabors license for the gap period if required |
| PdfSharpCore goes unmaintained | HIGH (2–4 weeks) | Activate `IPdfWriter` swap path; port `PdfSharpCoreWriter` to PDFsharp 6.x or a byte-level PDF writer; run golden corpus against new impl |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Box model misimplementation | v0.1 — before golden corpus committed | W3C CSS 2.1 conformance tests ≥95% pass on declared modules |
| AngleSharp.Css beta breakage | v0.1 — `ICssCascadeEngine` adapter seam from day 1 | Adapter boundary integration tests pass after every AngleSharp.Css version bump |
| Non-deterministic output | v0.1 — before first NuGet publish | Determinism canary CI step; cross-platform hash comparison |
| SSRF via IResourceResolver | v0.1 — `ThrowingResourceResolver` is the default | Security test: `file://` URI throws `PdfSecurityException` |
| Cross-tenant cache poisoning | v0.1 — `PdfCacheKey` sealed type from day 1 | Multi-tenant cache isolation test in CI |
| Vietnamese diacritic fallback | v0.1 — before Vietnamese golden snapshots committed | Startup font validation + visual inspection CI step |
| SixLabors.ImageSharp license | v0.1 M+1 — license audit checkpoint | Audit documented in `KNOWN-DEPENDENCIES.md` |
| PdfSharpCore stale maintenance | v0.1 — `IPdfWriter` genuinely swappable | Adapter swap integration test with a stub `IPdfWriter` |
| CSS policy gaps | v0.1 — policy allowlist from canonical source of truth | Rendering unsupported CSS throws `PdfPolicyException` |
| Bus factor on source generator | Hiring — begin M+3, not M+6 | Second FTE onboarded by M+5 |

---

## Sources

- CSS 2.1 specification, W3C: https://www.w3.org/TR/CSS21/ — margin collapsing, BFC, IFC rules
- W3C CSS 2.1 conformance test suite — authoritative baseline for declared modules
- AngleSharp.Css GitHub releases — beta status monitoring
- SixLabors.Fonts documentation — glyph metrics, subsetting API
- PdfSharpCore GitHub — maintenance status monitoring
- SixLabors commercial licensing threshold — documented in SixLabors.ImageSharp repository EULA
- PDF 1.7 specification (ISO 32000-1) — `/JavaScript`, `/EmbeddedFile`, linearization sections
- OWASP PDF Security guidance — SSRF, JS injection in PDF
- Domain experience: known failure modes in HtmlRenderer.PdfSharp (archived 2018), DinkToPdf/wkhtmltopdf CVE history, ExCSS and AngleSharp API instability patterns

---
*Pitfalls research for: Muonroi.Pdf — pure-managed HTML/CSS-to-PDF renderer, .NET open-core*
*Researched: 2026-05-26*
