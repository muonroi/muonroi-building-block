# Contributing to muonroi-building-block

This repository is the public .NET library foundation of the Muonroi ecosystem. Contributions are welcome, but "done" here means boundary-safe code, passing tests, and documentation that keeps pace with the behavior you changed.

## Before You Start

Read these first:

- [README.md](./README.md)
- [OSS-BOUNDARY.md](./OSS-BOUNDARY.md)
- https://docs.muonroi.com/docs/resources/CONTRIBUTING
- https://docs.github.com/en/site-policy/github-terms/github-community-code-of-conduct

Default working branch for this repo is `develop`.

## Local Prerequisites

Install:

- Git
- .NET SDK 8.0 or newer
- PowerShell 7 or newer for repo scripts
- PostgreSQL if you work on persistence-backed rule or decision table features
- Redis if you work on hot reload or distributed-cache behavior
- Node.js 20+ only if your change touches docs tooling or adjacent UI assets

## Clone And Build

```bash
git clone https://github.com/muonroi/muonroi-building-block.git
cd muonroi-building-block
dotnet restore Muonroi.BuildingBlock.sln
dotnet build Muonroi.BuildingBlock.sln -c Debug
```

## Run Tests

Run the full suite before opening a PR:

```bash
dotnet test Muonroi.BuildingBlock.sln -c Debug
```

Useful focused commands:

```bash
dotnet test tests/Muonroi.BuildingBlock.Test/Muonroi.BuildingBlock.Test.csproj -c Debug
dotnet test tests/Tenancy/Muonroi.Tenancy.Tests.csproj -c Debug
```

## Required Boundary Check

Do not add or change project references without checking the OSS boundary:

```bash
pwsh ./scripts/check-modular-boundaries.ps1 -RepoRoot .
```

Core rule:

- OSS packages must not reference commercial packages.
- Commercial packages may reference OSS packages.

## Coding Rules That Matter Here

These are not optional style preferences. They are ecosystem rules enforced by analyzers, review, or both.

### Runtime wrappers

- Use `IMDateTimeService` instead of raw `DateTime` in normal runtime code.
- Use `IMJsonSerializeService` instead of raw `JsonSerializer` unless you are doing byte-level or compatibility-specific work.
- Use `IMLog<T>` instead of `ILogger<T>` directly.
- Use `ISystemExecutionContextAccessor` instead of reaching for ambient static tenant state in new code.

### Data and EF

- Extend `MDbContext` for EF Core contexts in this ecosystem.
- Respect tenant-aware data rules when touching persistence models.

### Source-generator compatibility

For source-generator and compatibility-sensitive code:

- use `"\n"` instead of `Environment.NewLine`
- avoid `ToHashSet()` in netstandard2.0-sensitive code
- keep `IsExternalInit` compatibility in mind when records are involved

### Static-class exemptions

If a Roslyn analyzer exemption is justified for a static boundary, keep the exemption comment explicit and minimal:

```csharp
// MBB001-exempt: static-class boundary
```

## Documentation Requirement

If your change adds a feature or changes behavior, update docs in the shared docs repo:

- `../Docs/muonroi-docs/docs/03-guides/`
- `../Docs/muonroi-docs/docs/05-reference/`
- `../Docs/muonroi-docs/docs/06-resources/`

At minimum, document:

- the public API or option surface that changed
- any migration or operational impact
- new samples or workflows if the feature is user-facing

## Samples And Developer Experience

If your change affects developer onboarding, consider whether one of these also needs an update:

- [samples/README.md](./samples/README.md)
- sample-specific `README.md` files
- root [README.md](./README.md)
- docs links in the guides site

## Pull Request Checklist

Before opening a PR, make sure:

- code builds locally
- relevant tests pass
- modular boundaries still pass
- docs are updated if behavior changed
- generated code is regenerated, not hand-edited
- the PR description explains why the change exists

If your change touches public API, include:

- compatibility impact
- upgrade notes
- test coverage notes

## Review Expectations

Review will prioritize:

- behavioral regressions
- OSS/commercial boundary breaks
- missing tests
- missing docs
- misuse of ecosystem wrappers

Small, focused PRs get reviewed faster than broad mixed refactors.

## Good First Contributions

Look for issues labeled `good first issue` or `help wanted`. If GitHub Discussions is enabled for the repo, use it for design questions before opening large PRs.

Discussion templates live under [`.github/DISCUSSION_TEMPLATE`](./.github/DISCUSSION_TEMPLATE) so the repo is ready for that workflow once Discussions is enabled.

## Security

Do not disclose vulnerabilities in public issues. Follow the security guidance at:

- https://docs.muonroi.com/docs/resources/SECURITY

## Questions Before Coding

If you are unsure where code belongs:

- library/package code stays here
- deployed service code belongs in the private `muonroi-control-plane` or `muonroi-license-server` repos

That split is part of the open-core boundary. Keep it intact.
