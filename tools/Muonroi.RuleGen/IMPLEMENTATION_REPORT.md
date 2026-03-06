# RuleGen Upgrade Implementation Report

Reference plan: `UNIFIED_UPGRADE_PLAN.md`

## Status

- [x] Phase 1: Roslyn foundation.
- [x] Phase 2: Multi-file + DI + validation.
- [x] Phase 3: Config + watch + test scaffold.
- [x] Phase 4: Merge/split runtime workflow.
- [x] Phase 5: Enterprise hardening baseline.

## Implemented Commands

- `extract`
- `verify`
- `register`
- `generate-tests`
- `merge`
- `split`
- `watch`

## Core Technical Changes

1. Replaced regex parser with Roslyn-based extractor.
2. Generated rule body now maps actual method logic to `EvaluateAsync`.
3. Added source discovery from file/dir/project with glob filtering.
4. Added validation for duplicates, hookpoint validity, dependency cycles.
5. Added constructor DI generation from used handler fields.
6. Added runtime JSON import/export (`merge`/`split`) with FEEL/C# conversion helpers.
7. Added tenant/audit metadata in generated headers.
8. Added performance-oriented parallel extraction and generation timing.
9. Added VS Code extension scaffold (`vscode-extension/`) for merge/split/extract commands.
10. Added `merge` compile-check + rollback safety (`--compile-check`, `--compile-target`).
11. Added explicit class targeting for `merge`/`split` (`--class`).
12. Added integration tests for merge/split workflow (`tests/Muonroi.RuleGen.Tests`).

## Runtime Workflow Notes

- FEEL translation is best-effort for simple/medium expressions.
- Complex imperative blocks are flagged as custom and require review.

## Verification Summary

Manual end-to-end CLI checks completed successfully with sample project:
- `extract` -> generated rule classes
- `verify` -> expected files found
- `register` -> DI extension generated
- `merge` -> `*.Generated.cs` partial class produced
- `split` -> rule files + runtime JSON exported
- `merge` compile failure -> auto rollback of target and generated files

Automated integration tests:
- `Merge_WithCompileCheckFailure_ShouldRollbackTargetAndGeneratedFile`
- `Merge_WithClassTargeting_ShouldGenerateForSelectedClassOnly`
- `Split_WithClassTargeting_ShouldExportOnlySelectedClassRules`
- `Merge_WithCompileCheckSuccess_ShouldPersistGeneratedChanges`
