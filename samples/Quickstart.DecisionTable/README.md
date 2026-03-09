# Quickstart Decision Table Sample

Source path:

- `muonroi-building-block/samples/Quickstart.DecisionTable`

## What this demonstrates

- Minimal host wiring with `AddDecisionTableWeb(...)` and FEEL endpoints.
- Postgres-backed decision table store.
- Create and execute decision table via REST APIs.

## Prerequisite: local Postgres

```powershell
docker run --name muonroi-sample-postgres -e POSTGRES_USER=admin -e POSTGRES_PASSWORD=admin -e POSTGRES_DB=muonroi_decision_tables -p 5432:5432 -d postgres:16
```

## Quick run

```powershell
cd <workspace-root>\muonroi-building-block\samples\Quickstart.DecisionTable\src\Quickstart.DecisionTable.Api
dotnet restore
dotnet run
```

## Create a sample table

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/v1/decision-tables" -ContentType "application/json" -InFile ".\\assets\\discount-table.json"
```

## Execute the table

```powershell
$body = '{"inputs":{"amount":1200,"customerType":"premium","country":"US"}}'
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/v1/decision-tables/discount-rules/execute" -ContentType "application/json" -Body $body
```
