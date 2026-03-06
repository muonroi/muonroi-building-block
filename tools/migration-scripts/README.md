# Migration Scripts

Automation utilities for the modular migration plan.

## Scripts

- `downgrade-to-net8.ps1`: downgrades project target frameworks and key package versions to .NET 8.
- `generate-test-projects.ps1`: scaffolds test projects for each `src/Muonroi.*` package.
- `generate-sample-projects.ps1`: scaffolds sample projects for each `src/Muonroi.*` package.
- `generate-progress-tracker.ps1`: creates `migration-progress-tracker.csv`.
- `migrate-content-from-buildingblock.ps1`: copies content from `Muonroi.BuildingBlock` into new modular packages.
- `validate-m-prefix-standard.ps1`: validates domain-qualified `M*` naming taxonomy.

## Typical flow

```powershell
pwsh ./tools/migration-scripts/downgrade-to-net8.ps1
pwsh ./tools/migration-scripts/generate-test-projects.ps1
pwsh ./tools/migration-scripts/generate-sample-projects.ps1
pwsh ./tools/migration-scripts/generate-progress-tracker.ps1
pwsh ./tools/migration-scripts/migrate-content-from-buildingblock.ps1
pwsh ./tools/migration-scripts/validate-m-prefix-standard.ps1
```
