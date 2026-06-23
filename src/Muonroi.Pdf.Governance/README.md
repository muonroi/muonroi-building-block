# Muonroi.Pdf.Governance

> AngleSharp-backed HTML parser, CSS cascade engine, and policy enforcement layer for the Muonroi PDF rendering pipeline.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.Governance.svg)](https://www.nuget.org/packages/Muonroi.Pdf.Governance/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package provides the concrete implementations of the parsing, cascade, and policy contracts defined in `Muonroi.Pdf.Abstractions`. It ships an AngleSharp-powered HTML parser (`AngleSharpHtmlParser`), a CSS cascade engine (`AngleSharpCascadeEngine`), two ready-to-use CSS policies (`DefaultStrictPolicy` and `LegacyPrintPolicy`), and a signature-verification decorator (`SignedPdfCssPolicyDecorator`). All components are registered automatically when you call `AddPdf()` from `Muonroi.Pdf`.

## Installation

```bash
dotnet add package Muonroi.Pdf.Governance --prerelease
```

Most applications depend on `Muonroi.Pdf` (the full engine package), which already pulls in and registers this package. Add `Muonroi.Pdf.Governance` directly only when you need to reference its concrete types or swap a component before calling `AddPdf`.

## Quick Start

`AddPdf` (from `Muonroi.Pdf`) registers `AngleSharpHtmlParser`, `AngleSharpCascadeEngine`, and `LegacyPrintPolicy` via `TryAddSingleton`. Pre-registering your own implementation before `AddPdf` is the override mechanism.

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Extensions;            // AddPdf
using Muonroi.Pdf.Governance.Policies;   // DefaultStrictPolicy

var builder = WebApplication.CreateBuilder(args);

// Override the default LegacyPrintPolicy with the ultra-strict policy:
builder.Services.AddSingleton<IPdfCssPolicy, DefaultStrictPolicy>();

// AddPdf registers every remaining pipeline component (TryAddSingleton — won't replace the above).
builder.Services.AddPdf(builder.Configuration);
```

`appsettings.json` — configure limits and the policy gate:

```json
{
  "PdfConfigs": {
    "Limits": {
      "MaxHtmlBytes": 8388608,
      "MaxDomDepth": 256,
      "MaxElementCount": 100000
    },
    "Policy": {
      "SoftDegradeUnknownDisplay": false,
      "AllowModernLayout": false
    },
    "RequirePolicySignature": false
  }
}
```

## Features

- **AngleSharp HTML parser** — parses raw HTML into a DOM, enforcing `MaxHtmlBytes`, `MaxElementCount`, and `MaxDomDepth` limits before returning an `IParsedDocument`. Throws `PdfInputLimitException` on violation.
- **AngleSharp cascade engine** — applies an optional user stylesheet by injecting a `<style>` element into `<head>`, then wraps the result as an `AngleSharpStyledDocument` for the box-tree stage.
- **`DefaultStrictPolicy`** (`Id = "default-strict-v1"`) — blocks external `@import`, `@keyframes`, CSS transitions, `display:flex/grid`, `float`, `position:absolute/fixed/sticky`, `border-collapse:collapse`, `<script>` elements, non-http(s)/mailto link schemes, and the unsupported visual properties `filter`, `clip-path`, `backdrop-filter`, `mix-blend-mode` (raw-CSS scan; AngleSharp drops these from the CSSOM). Fail-loud on every violation.
- **`LegacyPrintPolicy`** (`Id = "legacy-print-v1"`) — CSS 2.1 print subset; relaxes `float`, `position:absolute`, and `border-collapse:collapse` relative to the strict policy. Optionally accepts `display:flex/grid` when `AllowModernLayout = true`. Soft-degrade mode (`SoftDegradeUnknownDisplay = true`) downgrades flex/grid violations from errors to warnings and falls back to `display:block`.
- **`SignedPdfCssPolicyDecorator`** — wraps any `IPdfCssPolicy`; when `RequirePolicySignature = true`, calls a provided `Func<bool>` verifier and throws `PdfPolicyException` (`gov.policy.signature-invalid`) on failure before delegating to the inner policy.
- **AOT-compatible** — `IsAotCompatible = true`; AngleSharp.Css trim warnings originate in AngleSharp internals and are mitigated via `TrimmerRootDescriptor` in the AOT sample.

## Configuration

All tunables live under the `PdfConfigs` section (bound by `AddPdf` with `ValidateOnStart`):

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `PdfConfigs:Limits:MaxHtmlBytes` | `long` | `8388608` | Maximum HTML input size in bytes |
| `PdfConfigs:Limits:MaxDomDepth` | `int` | `256` | Maximum DOM nesting depth |
| `PdfConfigs:Limits:MaxElementCount` | `int` | `100000` | Maximum element count after parsing |
| `PdfConfigs:Policy:SoftDegradeUnknownDisplay` | `bool` | `false` | Downgrade `display:flex/grid` to warning + block fallback (LegacyPrintPolicy only) |
| `PdfConfigs:Policy:AllowModernLayout` | `bool` | `false` | Accept flex/grid when the engine's `FlexLayoutEngine` is active (LegacyPrintPolicy only) |
| `PdfConfigs:RequirePolicySignature` | `bool` | `false` | Require a valid policy-config signature before validation proceeds |

`PdfPolicySettings` is the strongly typed options class for the `Policy` sub-section. `PdfConfigs.PdfLimits` is the strongly typed options class for `Limits`.

## API Reference

| Type | Purpose |
|------|---------|
| `AngleSharpHtmlParser` | Implements `IHtmlParser`; parses HTML + enforces structural limits |
| `AngleSharpCascadeEngine` | Implements `ICssCascadeEngine`; injects user stylesheet + wraps DOM |
| `DefaultStrictPolicy` | Implements `IPdfCssPolicy`; ultra-strict gate, all unsupported CSS is an error |
| `LegacyPrintPolicy` | Implements `IPdfCssPolicy`; CSS 2.1 print subset with optional soft-degrade / modern-layout modes |
| `SignedPdfCssPolicyDecorator` | Decorator around `IPdfCssPolicy`; adds signature-verification gate |
| `PdfPolicySettings` | Options record bound from `PdfConfigs:Policy`; controls `SoftDegradeUnknownDisplay` and `AllowModernLayout` |
| `PdfConfigs` | Root configuration class bound from the `PdfConfigs` section |
| `PdfConfigs.PdfLimits` | Nested options record; controls structural input limits |

## Samples

No dedicated sample ships for this package. See the full engine sample for end-to-end usage:

- [Muonroi.Pdf.Samples](../../samples/Muonroi.Pdf.Samples/) — demonstrates `AddPdf` registration, font configuration, and PDF rendering

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Pdf.Abstractions`](../Muonroi.Pdf.Abstractions/) — contracts (`IPdfCssPolicy`, `IHtmlParser`, `ICssCascadeEngine`, `IPdfDocumentContext`)
- [`Muonroi.Pdf`](../Muonroi.Pdf/) — full engine package; calls `AddPdf` which registers all governance components
- [`Muonroi.Pdf.Enterprise`](../Muonroi.Pdf.Enterprise/) — enterprise extensions; decorates the policy and cascade registrations with commercial features

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
