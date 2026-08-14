> Quickstart demonstrating the Governance Abstractions, specifically Licensing features.

## What This Sample Demonstrates
- Registration and usage of `ILicenseGuard` and `ILicenseStore` abstractions
- Feature gate pattern (`EnsureFeature`)
- Checking license tiers (`LicenseTier`)
- Offline license handling concepts via `LicensePayload`

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Governance.Abstractions/src/Quickstart.Governance.Abstractions.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `Program.cs` — service registration (in-memory mock) and endpoint wiring
- `Controllers/LicenseController.cs` — API endpoints demonstrating feature gates

## How It Works
The API uses mocked implementations of `ILicenseGuard` and `ILicenseStore` to showcase how to protect endpoints based on license tiers and available features without requiring the full governance runtime.
