# Quickstart.Tenancy.Core
> Demonstrates core multi-tenancy primitives: ID resolution, schema mapping, validation, and quotas.

## What This Sample Demonstrates
- ITenantContext and DefaultTenantIdResolver
- TenantSchemaSelector to map tenant ID to schema
- TenantSecurityValidator to protect against DB schema injection
- TenantQuotaTracker for per-tenant rate limits/quotas

## Prerequisites
- .NET 8 SDK

## Run

`ash
cd samples/Quickstart.Tenancy.Core/src/Quickstart.Tenancy.Core.Api
dotnet run
`

Then open http://localhost:5000/swagger.
Pass header X-Tenant-ID: tenant1 to endpoints.

## Key Files
- Program.cs — service registration and endpoint wiring
