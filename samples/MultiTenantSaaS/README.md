# MultiTenantSaaS Sample

## What this demonstrates

- Tenant-specific rule registration with `[TenantRuleGroup("pricing", "<tenant>")]`.
- One API endpoint (`POST /api/pricing/{tenantId}`) producing different outcomes for 3 tenants.
- Optional enterprise control-plane wiring with `AddMRuleEngineWithPostgres` and Redis hot reload.

## Prerequisites

- .NET 8 SDK
- Optional for control-plane mode: Docker + PostgreSQL + Redis

## Quick start

```powershell
cd src/MultiTenant.Api
dotnet restore
dotnet run
```

Try the same payload for different tenants:

```powershell
curl -X POST http://localhost:5000/api/pricing/tenant-starter -H "Content-Type: application/json" -d '{"basePrice":20,"seatCount":30,"annualCommitment":false}'
curl -X POST http://localhost:5000/api/pricing/tenant-pro -H "Content-Type: application/json" -d '{"basePrice":20,"seatCount":30,"annualCommitment":false}'
curl -X POST http://localhost:5000/api/pricing/tenant-enterprise -H "Content-Type: application/json" -d '{"basePrice":20,"seatCount":30,"annualCommitment":false}'
```

You should see different `plan`, `appliedMultiplier`, and `finalPrice` values.

## Enable Control Plane + Redis (optional)

1. Set `ControlPlane:Enabled` to `true` in `appsettings.json`.
2. Configure `ConnectionStrings:RuleEngineDb` and `ConnectionStrings:RuleEngineRedis`.
3. Restart the service.

When enabled, the sample wires:

- `AddMRuleEngineWithPostgres(...)`
- `AddMRuleEngineWithRedisHotReload(...)`

## Learn more

- [Multi-Tenant Guide](../../../../Docs/muonroi-docs/docs/03-guides/multi-tenancy/multi-tenant-guide.md)
- [Canary Rollout Guide](../../../../Docs/muonroi-docs/docs/03-guides/control-plane/canary-rollout-guide.md)
