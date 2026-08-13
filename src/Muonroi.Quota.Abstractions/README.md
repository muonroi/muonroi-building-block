# Muonroi.Quota.Abstractions

## Description
Contains the core interfaces and abstractions for quota management, rate limiting, and usage tracking in the Muonroi ecosystem.

## Features
- Standardized `IQuotaManager` interface.
- Tenant-level and user-level quota abstractions.
- Extensible limit policies.

## Minimal Usage
```csharp
var quotaManager = serviceProvider.GetRequiredService<IQuotaManager>();
bool canProceed = await quotaManager.CheckQuotaAsync("api_calls", tenantId);
```
