# Muonroi.RuleGen Unified Upgrade Plan (Merged from `UPGRADE_PLAN_V2.md` + `toolkit_upgrade.md`)

## Goals
- Replace fragile regex parsing with Roslyn AST/semantic-safe extraction.
- Make RuleGen production-ready for large codebases (multi-file/project, validation, test scaffold).
- Support bidirectional workflow:
  - code -> generated rules
  - runtime JSON rules -> handler code (merge)
  - handler code -> runtime JSON rules (split)
- Add enterprise-grade capabilities (tenant-aware generation, audit metadata, performance, observability).

## Phase Structure

## Phase 1: Roslyn Foundation (Critical)
- Roslyn parser for `[MExtractAsRule]` methods.
- Full method body extraction (remove TODO placeholder generation).
- Generate `EvaluateAsync` with return-type mapping (`RuleResult`, `bool`, `Task`, `Task<T>`).
- Preserve custom attributes, comments, async patterns.

Exit Criteria:
- Generated `.g.cs` files compile without manual TODO replacement.

## Phase 2: Multi-File + DI + Validation
- `--source`, `--source-dir`, `--project` discovery with glob include/exclude.
- Detect class field dependencies used by extracted methods and generate constructor injection.
- Validation for duplicate codes, invalid HookPoint, missing dependencies, circular `DependsOn`.
- Optional test scaffold generation.

Exit Criteria:
- Can process full project folders and fail-fast on invalid rule graphs.

## Phase 3: Developer Experience
- `.rulegenrc.json` / `.rulegen.json` support.
- `watch` command for auto-regeneration on source changes.
- `generate-tests` command for xUnit scaffolds.

Exit Criteria:
- Team can run standardized generation via config and watch mode.

## Phase 4: Merge/Split Runtime Workflow
- `merge` command:
  - import runtime JSON rules,
  - generate/update partial class `*.Generated.cs`,
  - conflict strategy: `append|replace|interactive`.
- `split` command:
  - extract handler methods back to rule files,
  - export runtime JSON payload.
- Basic FEEL <-> C# translation for condition/action expressions.

Exit Criteria:
- Round-trip workflow works for simple/medium rule patterns.

## Phase 5: Enterprise Hardening
- Tenant-aware metadata (`--tenant`) in generated artifacts.
- Audit metadata (`GeneratedAt`, `Author`, git commit).
- Parallel extraction and timing metrics.
- Documentation + troubleshooting for unsupported complex translation patterns.

Exit Criteria:
- CLI suitable for enterprise CI/CD pipelines with traceability.

## Implementation Order
1. Phase 1 -> docs update.
2. Phase 2 -> docs update.
3. Phase 3 -> docs update.
4. Phase 4 -> docs update.
5. Phase 5 -> docs update.

## Notes on Translation Boundaries
- Complex imperative C# (loops/LINQ/external IO) cannot always convert cleanly to FEEL.
- Tool emits best-effort conversion and marks custom/untranslatable logic for developer review.
