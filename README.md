# muonroi-building-block

Muonroi Building Block is the .NET foundation of the Muonroi open-core ecosystem: rule engine, decision tables, governance, tenancy, observability, and the commercial extensions that sit on top of the OSS base.

[![CI](https://github.com/muonroi/muonroi-building-block/actions/workflows/ci.yml/badge.svg)](https://github.com/muonroi/muonroi-building-block/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/muonroi/muonroi-building-block/branch/develop/graph/badge.svg)](https://codecov.io/gh/muonroi/muonroi-building-block)
[![OSS License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](./LICENSE-APACHE)
[![Commercial License](https://img.shields.io/badge/commercial-available-blue.svg)](./LICENSE-COMMERCIAL)

## Install

```bash
dotnet add package Muonroi.RuleEngine.Core
dotnet add package Muonroi.RuleEngine.SourceGenerators
```

## Quick Example

Annotate rule logic in normal C#:

```csharp
[MExtractAsRule("HIGH_VALUE_ORDER", DependsOn = new[] { "CREDIT_SCORE" })]
public RuleResult HighValue(OrderContext context, FactBag facts)
{
    if (context.Amount <= 1000m) return RuleResult.Failure("Below threshold.");
    facts["requiresReview"] = true;
    return RuleResult.Success();
}
```

Generate the registration and wire the engine:

```bash
muonroi-rule extract --source src/Rules --output Generated/Rules
muonroi-rule register --rules Generated/Rules --output Generated/RuleEngineRegistrationExtensions.g.cs
```

```csharp
using Muonroi.RuleEngine.Generated;

builder.Services.AddRuleEngine<OrderContext>();
builder.Services.AddGeneratedRules();
```

Result: the rule enters the DI graph without a handwritten registration block, and `muonroi-rule verify` can catch dependency and rule-code mistakes before runtime.

## Package Families

| Area | OSS packages | Commercial packages |
| --- | --- | --- |
| Core | `Muonroi.Core.Abstractions`, `Muonroi.Core`, `Muonroi.Logging`, `Muonroi.Logging.Abstractions` | - |
| Governance | `Muonroi.Governance.Abstractions`, `Muonroi.Governance` | `Muonroi.Governance.Enterprise` |
| Rule engine | `Muonroi.RuleEngine.Abstractions`, `Muonroi.RuleEngine.Core`, `Muonroi.RuleEngine.SourceGenerators`, `Muonroi.RuleEngine.DecisionTable`, `Muonroi.RuleEngine.Testing` | `Muonroi.RuleEngine.Runtime.Web`, `Muonroi.RuleEngine.DecisionTable.Web`, `Muonroi.RuleEngine.CEP`, `Muonroi.UiEngine.Catalog` |
| Infrastructure | `Muonroi.AspNetCore`, `Muonroi.Tenancy`, `Muonroi.Observability`, `Muonroi.Data.*` | `Muonroi.Caching.Redis`, `Muonroi.SignalR`, `Muonroi.Bff`, `Muonroi.AuthZ`, more |

The boundary rule remains simple:

- OSS packages must not reference commercial packages.
- Commercial packages may reference OSS packages.

See [OSS-BOUNDARY.md](./OSS-BOUNDARY.md) for the detailed matrix.

## What To Read First

- Docs: https://docs.muonroi.com/docs/getting-started/introduction
- Rule engine guide: https://docs.muonroi.com/docs/guides/rule-engine/rule-engine-guide
- Rule source generator deep dive: https://docs.muonroi.com/docs/guides/rule-engine/rule-source-generator
- Decision table guide: https://docs.muonroi.com/docs/guides/rule-engine/decision-table-guide
- Samples index: [samples/README.md](./samples/README.md)

## Samples

- [Quickstart.RuleEngine](./samples/Quickstart.RuleEngine/README.md)
- [Quickstart.DecisionTable](./samples/Quickstart.DecisionTable/README.md)
- [FraudDetection](./samples/FraudDetection/README.md)
- [LoanApproval](./samples/LoanApproval/README.md)
- [MultiTenantSaaS](./samples/MultiTenantSaaS/README.md)
- [RuleSourceGen](./samples/RuleSourceGen/README.md)

## Local Development

```bash
dotnet restore Muonroi.BuildingBlock.sln
dotnet build Muonroi.BuildingBlock.sln -c Debug
```

Useful gates before opening a PR:

```bash
pwsh ./scripts/check-modular-boundaries.ps1 -RepoRoot .
dotnet test Muonroi.BuildingBlock.sln -c Debug
```

## Community

- Docs: https://docs.muonroi.com
- Issues: https://github.com/muonroi/muonroi-building-block/issues
- Contributing guide: [CONTRIBUTING.md](./CONTRIBUTING.md)
- Security policy: https://docs.muonroi.com/docs/resources/SECURITY

Discussion templates are prepared under [`.github/DISCUSSION_TEMPLATE`](./.github/DISCUSSION_TEMPLATE) and become active as soon as GitHub Discussions is enabled for the repository.

## License

OSS packages are Apache 2.0. Commercial packages are distributed under the Muonroi commercial license and require activation proof in deployed environments.
