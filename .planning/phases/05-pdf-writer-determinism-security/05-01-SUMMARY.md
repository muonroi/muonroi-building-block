---
phase: 05-pdf-writer-determinism-security
plan: 01
subsystem: pdf
tags: [security, pdfsharpcore, policy-gate, resource-resolver, csharp]

# Dependency graph
requires:
  - phase: 01-abstractions-contracts
    provides: PdfException hierarchy, IResourceResolver/ResourceResult contracts
  - phase: 02-parse-cascade-policy
    provides: DefaultStrictPolicy and PolicyViolation pipeline
provides:
  - PdfSecurityException typed exception for security violations
  - ThrowingResourceResolver default resolver blocking file:// and javascript:
  - forbidden.script-element policy violation (SEC-05)
  - PdfSharpCore package reference in Muonroi.Pdf
affects: [pdf-writer, determinism, security-tests]

# Tech tracking
tech-stack:
  added: [PdfSharpCore 1.3.65]
  patterns:
    - "Security violations surface as PdfSecurityException(ruleId, detail) following the base PdfException constructor pattern"
    - "Safe-by-default resolver: disallowed schemes throw, unknown schemes return null (no external fetch)"

key-files:
  created:
    - src/Muonroi.Pdf.Abstractions/Exceptions/PdfSecurityException.cs
    - src/Muonroi.Pdf/Internal/Security/ThrowingResourceResolver.cs
  modified:
    - src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs
    - src/Muonroi.Pdf/Muonroi.Pdf.csproj

key-decisions:
  - "http/https/data schemes return null (unavailable) rather than throw; only file:// and javascript: are SEC-06 violations"
  - "Single forbidden.script-element violation emitted (break on first match) to avoid enumerating every script tag"

patterns-established:
  - "Pattern 1: Internal security primitives live under src/Muonroi.Pdf/Internal/Security/"
  - "Pattern 2: Policy element-security scanning runs as Pass 3 after computed-style passes"

requirements-completed: [SEC-05, SEC-06]

# Metrics
duration: 12min
completed: 2026-05-27
---

# Phase 5 Plan 01: Security Foundation Summary

**PdfSecurityException, a safe-by-default ThrowingResourceResolver blocking file://, script-element policy rejection, and the PdfSharpCore reference needed by the writer.**

## Performance

- **Duration:** ~12 min
- **Tasks:** 2
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments
- `PdfSecurityException` extends `PdfException` using the standard `(ruleId, detail)` constructor pattern
- `ThrowingResourceResolver` throws `PdfSecurityException("SEC-06", ...)` for `file://` and `javascript:` URIs, returns null for all other schemes (no external fetch by default)
- `DefaultStrictPolicy` emits a `forbidden.script-element` violation (SEC-05) before any layout occurs
- `Muonroi.Pdf` now references `PdfSharpCore` (version 1.3.65 via Central Package Management — no inline Version)

## Task Commits

1. **Task 1: PdfSecurityException + ThrowingResourceResolver** - `acce5af` (feat)
2. **Task 2: Script element policy rejection + PdfSharpCore reference** - `c21641e` (feat)

## Files Created/Modified
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfSecurityException.cs` - Typed security-violation exception
- `src/Muonroi.Pdf/Internal/Security/ThrowingResourceResolver.cs` - Default scheme-blocking resolver
- `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` - Added Pass 3 script-element scan
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` - Added PdfSharpCore PackageReference

## Decisions Made
- Matched the `IResourceResolver.ResolveAsync` signature exactly (`contentTypeHint = null`, `cancellationToken = default`), rather than the plan's shorthand `ct` parameter name — required to implement the interface.
- Returned `null` (not throw) for http/https/data per the plan's SEC-06 scope note.

## Deviations from Plan

**1. [Rule 3 - Blocking] Resolver parameter names aligned to the interface contract**
- **Found during:** Task 1
- **Issue:** Plan pseudo-signature used `ct`; the `IResourceResolver` contract declares `contentTypeHint = null, CancellationToken cancellationToken = default`.
- **Fix:** Implemented the method with the exact contract parameter names and default values.
- **Files modified:** src/Muonroi.Pdf/Internal/Security/ThrowingResourceResolver.cs
- **Verification:** Muonroi.Pdf builds with 0 errors.
- **Committed in:** acce5af

**Build-environment note (not a code deviation):** The Bash/Git-Bash shell on this Windows host could not create MSBuild `obj` directories (path-translation/`GenerateTargetFrameworkMonikerAttribute` node-reuse contention). Builds were run via PowerShell with single-node MSBuild (`-m:1 -nodereuse:false`). No project files or target frameworks were changed.

---

**Total deviations:** 1 auto-fixed (1 blocking, contract alignment)
**Impact on plan:** Necessary to satisfy the interface contract. No scope creep.

## Issues Encountered
- MSBuild obj-creation failures under the default Bash sandbox; resolved by building through PowerShell. All three target projects (Abstractions, Pdf, Governance) build with 0 errors.

## Verification
- `dotnet build src/Muonroi.Pdf.Abstractions/...` — 0 errors
- `dotnet build src/Muonroi.Pdf/...` — 0 errors (PdfSharpCore 1.3.65 restored)
- `dotnet build src/Muonroi.Pdf.Governance/...` — 0 errors

## Self-Check: PASSED

## Next Phase Readiness
- Security primitives and PdfSharpCore reference are in place; the PDF writer class can now be implemented against `ThrowingResourceResolver` and `PdfSecurityException`.
- No blockers. Honored existing contracts: Abstractions stays netstandard2.0, no Strict/Relaxed statics re-added, PdfRenderResult.Diagnostics untouched.

---
*Phase: 05-pdf-writer-determinism-security*
*Completed: 2026-05-27*
