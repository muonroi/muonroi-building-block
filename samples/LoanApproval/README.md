# LoanApproval Sample

## What this demonstrates

- Code-first rule orchestration with `IRule<TContext>` and `RuleOrchestrator<TContext>`.
- Rule extraction markers via `[MExtractAsRule]` (`CREDIT_SCORE`, `DEBT_RATIO`).
- A practical `/api/loans` endpoint that returns a business decision payload.
- Ready-to-import artifacts: `rulesets/loan-approval.json` and `decision-tables/loan-tiers.json`.

## Prerequisites

- .NET 8 SDK

## Quick start

```powershell
cd src/LoanApproval.Api
dotnet restore
dotnet run
```

Call the API:

```powershell
curl -X POST http://localhost:5000/api/loans \
  -H "Content-Type: application/json" \
  -d '{"applicantId":"A-100","creditScore":735,"monthlyIncome":4200,"monthlyDebt":1400,"requestedAmount":50000,"employmentMonths":18}'
```

Expected result:

- `approved: true`
- `tier: "standard"`

## Connect to Control Plane (optional)

1. Import `rulesets/loan-approval.json` into your control plane ruleset UI.
2. Import `decision-tables/loan-tiers.json` into decision table management.
3. Update thresholds and observe output differences when posting to `/api/loans`.

## Learn more

- [Rule Engine Guide](../../../../Docs/muonroi-docs/docs/03-guides/rule-engine/rule-engine-guide.md)
- [Decision Table Guide](../../../../Docs/muonroi-docs/docs/03-guides/rule-engine/decision-table-guide.md)
