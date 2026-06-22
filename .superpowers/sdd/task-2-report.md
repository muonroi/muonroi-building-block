# Task 2 Report — MSTD0001 Forbidden-Throw Analyzer

**Status:** DONE
**Commit:** 46ee85b1
**Test result:** 5/5 passed

## Files created
- `src/Muonroi.CodeStandards/Analyzers/Mstd0001_ForbiddenThrowAnalyzer.cs` — analyzer implementation
- `tests/Muonroi.CodeStandards.Tests/Muonroi.CodeStandards.Tests.csproj` — test project (CPM, no version attributes)
- `tests/Muonroi.CodeStandards.Tests/Mstd0001_ForbiddenThrowAnalyzerTests.cs` — 5 xunit facts covering: raw throw in Muonroi ns (error), throw expression in Muonroi ns (error), MException-derived throw (ok), non-Muonroi ns (ok), test assembly (ok)

## Concerns
- RS1038 warning on `Muonroi.CodeStandards` project (Workspaces.Common ref incompatible with compiler extension). This is from Task 1's `.csproj` (`Microsoft.CodeAnalysis.Workspaces.Common` reference) — not introduced here. Left as-is per task instructions.
