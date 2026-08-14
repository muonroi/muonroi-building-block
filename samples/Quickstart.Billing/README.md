# Quickstart.Billing
> Demonstrates canonical metered billing and invoice aggregation.

## What This Sample Demonstrates
- `IBillingProvider` registration and event recording
- `IUsageAggregator` to roll usage into priced line items
- `BillableEvent` tracking with a `PricingPlan`
- In-memory `ITenantQuotaStore` implementation

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Billing/src/Quickstart.Billing.Api
dotnet run
```

Then open:
- API/Swagger: http://localhost:5000/swagger

## Key Files
- `Program.cs` — Service registration
- `Services/InMemoryTenantQuotaStore.cs` — Sample quota store
- `Controllers/BillingController.cs` — API endpoints for recording events and previewing invoices

## How It Works
The standard `AddRecordOnlyBilling()` extension registers a record-only `IBillingProvider` and the default `UsageAggregator`. A custom `ITenantQuotaStore` stores the metered usage, which is later aggregated and previewed without any external payment-processor calls.
