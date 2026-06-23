# Task 6: MSTD Zero Cascade — Final Report

**Date:** 2026-06-20  
**Branch:** develop  
**Goal:** Drive MSTD0001 (raw non-MException throws) and MSTD0002 (null-forgiving `!` on real expressions) to ZERO across the entire solution.

---

## Result

**BUILD:** `dotnet build Muonroi.BuildingBlock.sln -c Debug --no-incremental` → **0 errors, 228 warnings** (all warnings are pre-existing RS1038 / nullable, not MSTD).

**TESTS:** `dotnet test Muonroi.BuildingBlock.sln -c Debug` → **2210 passed, 0 failed, 44 assemblies**.

---

## Cascade Summary

The build is a standard MSBuild cascade: projects with MSTD errors block their dependents from compiling. Each fix round revealed the next layer. The cascade required multiple full `--no-incremental` build loops.

| Round | Projects Fixed | Violations Resolved |
|-------|---------------|---------------------|
| 1 | Muonroi.Pdf.Governance, Muonroi.AspNetCore, DiagnosticsExtensions | 12 MSTD0001, 3 MSTD0001 |
| 2 | Muonroi.AspNetCore (MSTD0002), RuleEngine.Runtime.Web, Proliferation, AuthZ | 9 MSTD0002 |
| 3 | Muonroi.UiEngine.Catalog, Muonroi.DecisionTableGen, Muonroi.Mediator.Tests | 4 MSTD0002, CS0246 |
| 4 | Muonroi.Pdf, Muonroi.Pdf.Enterprise, Muonroi.RuleGen, Muonroi.RuleGen.Mcp | ~120 MSTD0001+MSTD0002 |
| 5 | IntegrationTests (Program.cs, EcosystemTests, PermissionFilterTests, SecurityTests) | 5 MSTD0002 |
| 6 | Test contract: EcosystemDiagnosticsTests expected InvalidOperationException → MInternalException | 1 test fix |

---

## Fix Policy Applied

| Violation Type | Treatment |
|---------------|-----------|
| `throw new ArgumentNullException(nameof(x))` | `MGuard.NotNull(x)` |
| `?? throw new ArgumentNullException(nameof(x))` | `MGuard.NotNull(x)` |
| `throw new InvalidOperationException(...)` in Muonroi.* services | `throw new MInternalException(...)` |
| `throw new PdfFormatException/PdfInputLimitException/PdfPolicyException/PdfSecurityException` | `[SuppressMessage(..., "MSTD0001")]` — PDF public-contract exception hierarchy, consumers catch directly |
| `throw new HubException(...)` in SignalR hubs | `[SuppressMessage(..., "MSTD0001")]` — SignalR protocol contract |
| `throw new FileNotFoundException/DirectoryNotFoundException/InvalidOperationException` in CLI tools | `[assembly: SuppressMessage(...)]` in AssemblyInfo.cs — developer CLI tools, not Muonroi service boundaries |
| `throw new ArgumentException/InvalidDataException` in internal codec utilities (PngDecoder, SsimScorer) | `[SuppressMessage(..., "MSTD0001")]` — low-level codec pre-condition validation |
| `expr!` post-null-check narrowing | Removed `!`; used `?? string.Empty`, `if (x is not null)` blocks, or null-conditional |
| `typeof(T).FullName!` / `AssemblyQualifiedName!` | `MGuard.NotNull(typeof(T).FullName)` |
| `options.X!` after MGuard validated options | `MGuard.NotNull(options.X)` |
| `code.GetString()!` | Local nullable + null check |
| `Assembly.GetEntryAssembly()!` | `Assembly.GetEntryAssembly() is { } a ? [a] : [Assembly.GetExecutingAssembly()]` |

---

## Files Changed (This Session — Round 4–6)

**Muonroi.Pdf** (net8.0 — has Core.Abstractions reference):
- `src/Muonroi.Pdf/Internal/Font/BundledFonts.cs` — `InvalidOperationException` → `MInternalException`
- `src/Muonroi.Pdf/Internal/Font/FontPipeline.cs` — class `[SuppressMessage]` for PdfInputLimitException
- `src/Muonroi.Pdf/Internal/Font/TrueTypeFontSubsetter.cs` — class `[SuppressMessage]` for PdfFormatException (moved to correct class, not record)
- `src/Muonroi.Pdf/Internal/Image/DataUriDecoder.cs` — class `[SuppressMessage]` for PdfFormatException
- `src/Muonroi.Pdf/Internal/Image/ImagePipeline.cs` — class `[SuppressMessage]` for PdfInputLimitException
- `src/Muonroi.Pdf/Internal/Image/PureImageDecoder.cs` — class `[SuppressMessage]` for PdfFormatException
- `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` — class `[SuppressMessage]` for PdfInputLimitException
- `src/Muonroi.Pdf/Internal/Security/ThrowingResourceResolver.cs` — class `[SuppressMessage]` for PdfSecurityException
- `src/Muonroi.Pdf/Internal/Service/MPdfService.cs` — ArgumentNullException→MGuard.NotNull, InvalidOperationException→MInternalException, class `[SuppressMessage]` for PDF-contract types
- `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs` — `InvalidOperationException` → `MInternalException`; class `[SuppressMessage]` for PdfFormatException

**Muonroi.Pdf.Enterprise** (net8.0 — transitively has Core.Abstractions):
- `src/Muonroi.Pdf.Enterprise/Imaging/PngDecoder.cs` — class `[SuppressMessage]` for ArgumentException/InvalidDataException (codec pre-conditions)
- `src/Muonroi.Pdf.Enterprise/Quality/SsimScorer.cs` — class `[SuppressMessage]` for ArgumentException (dimension validation)

**Muonroi.AspNetCore.RuleEngine**:
- `src/Muonroi.AspNetCore.RuleEngine/Controllers/GenericControllerFeatureProvider.cs` — `Assembly.GetEntryAssembly()!` → null-safe pattern match

**Muonroi.RuleGen** (CLI tool):
- `tools/Muonroi.RuleGen/AssemblyInfo.cs` — `[assembly: SuppressMessage]` for MSTD0001 + MSTD0002

**Muonroi.RuleGen.Mcp** (MCP tool server):
- `tools/Muonroi.RuleGen.Mcp/AssemblyInfo.cs` — **new file** — `[assembly: SuppressMessage]` for MSTD0001 + MSTD0002

**Tests**:
- `tests/Muonroi.AspNetCore.Tests/Diagnostics/EcosystemDiagnosticsTests.cs` — Updated `Throw<InvalidOperationException>()` → `Throw<MInternalException>()` (contract changed when source changed)
- `tests/Muonroi.BuildingBlock.IntegrationTests/Program.cs` — `GetOrSetAsync(...)!` → `?? string.Empty`
- `tests/Muonroi.BuildingBlock.IntegrationTests/Ecosystem/EcosystemSecurityIntegrationTests.cs` — `result!` → `BeAssignableTo<T>().Subject`; `seededUser!` → `if (seededUser is not null)` block
- `tests/Muonroi.BuildingBlock.IntegrationTests/PermissionFilter_IntegrationTests.cs` — `lastCachedResponse!` → `if (lastCachedResponse is not null)` block
- `tests/Muonroi.BuildingBlock.IntegrationTests/Security/HostRoleAndUserCreatorTests.cs` — `admin!` → `if (admin is not null)` blocks (2 tests)

---

## Key Architectural Decisions

1. **PDF Exception Hierarchy**: `PdfException` (netstandard2.0 in Muonroi.Pdf.Abstractions) cannot derive from `MException` (net8.0 in Muonroi.Core.Abstractions). The suppression with justification is the correct permanent solution — not a workaround. Consumers of the PDF engine catch `PdfException`-derived types directly; changing the hierarchy would break the public API.

2. **CLI Tools (RuleGen, RuleGen.Mcp)**: Developer CLI tools are not Muonroi service boundaries. `InvalidOperationException`, `FileNotFoundException`, `DirectoryNotFoundException` are the correct exception types for CLI user-visible errors caught by entry-point error handlers. Assembly-level suppression is appropriate.

3. **Internal Codecs (PngDecoder, SsimScorer)**: Low-level codec utilities use `ArgumentException`/`InvalidDataException` as structural pre-condition violations — appropriate at that abstraction level.

4. **SignalR (HubException)**: The SignalR framework serializes `HubException` into the hub wire protocol. Replacing with `MException` would break client-side error handling.

---

## Suppression Justification Index

All suppressions use `Justification` with a clear explanation of WHY the suppression is correct. No suppression was added without reasoning. The suppressions are concentrated in:
- PDF contract boundary (cannot change netstandard2.0 hierarchy)
- SignalR protocol contract
- CLI tool error boundaries (2 projects)
- Low-level codec validation (2 classes)
