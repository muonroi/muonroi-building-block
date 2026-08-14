# Quickstart.Pdf.Governance
> Demonstrates CSS policy enforcement and Modern Layout (Flexbox/Grid) governance in Muonroi.Pdf.

## What This Sample Demonstrates
- Enforcing `DefaultStrictPolicy` to completely block unauthorized CSS like `display: grid` or `display: flex`.
- Enabling the `AllowModernLayout` flag to allow Flexbox/Grid via `LegacyPrintPolicy`.
- Injecting policies per render using `PdfRenderOptions`.

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Pdf.Governance/src/Quickstart.Pdf.Governance.Api
dotnet run
```

Then open [http://localhost:5000/swagger](http://localhost:5000/swagger).

- Execute `POST /pdf/render/modern` to see the engine render a PDF using CSS Flexbox (allowed because `AllowModernLayout` is true in `appsettings.json`).
- Execute `POST /pdf/render/strict` to see the engine throw a `PdfPolicyException` (blocked by `DefaultStrictPolicy`).

## Key Files
- `Program.cs` — explicit policy service registration and per-render assignment
- `appsettings.json` — enables modern layout in the configuration

## How It Works
The `Muonroi.Pdf` engine includes a robust CSS policy gate. By default, unknown or complex layout instructions may be blocked to maintain deterministic layout guarantees or fail-fast compliance. 
- The `DefaultStrictPolicy` is extremely rigid, throwing on any flex/grid use.
- The `LegacyPrintPolicy` can selectively accept modern CSS (Flex/Grid) based on the `AllowModernLayout` flag bound via `PdfConfigs`. 
You can pass the desired policy per call via `PdfRenderOptions.Policy`.
