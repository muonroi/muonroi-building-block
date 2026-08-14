# Muonroi.Pdf.Governance

> CSS policy enforcement and HTML/CSS parsing adapters for Muonroi PDF rendering.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.Governance.svg)](https://www.nuget.org/packages/Muonroi.Pdf.Governance/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Pdf.Governance` package serves as the gateway and security layer for the Muonroi PDF rendering engine. Before the `Muonroi.Pdf` layout engine ever sees a DOM tree, this package parses the raw HTML/CSS using AngleSharp, cascades the stylesheets, and strictly sanitizes the resulting computed styles against configurable policies.

By stripping out unhandled, unsupported, or maliciously crafted CSS directives, this package ensures that PDF generation remains deterministic, memory-safe, and visually consistent across environments.

## Features

- **AngleSharp Integration**: Acts as the HTML parser (`AngleSharpHtmlParser`) and CSS cascading engine (`AngleSharpCascadeEngine`) adapter, translating raw text into the AST expected by the PDF layout engine.
- **Strict CSS Whitelisting**: Implements `IPdfCssPolicy` to evaluate every CSS property declaration.
- **LegacyPrintPolicy**: The default policy that accepts traditional block/inline layouts and tables, silently stripping unsupported properties.
- **Modern Layout Gating**: Explicit gates for CSS Flexbox and CSS Grid. If the system configuration `AllowModernLayout` is false, this package rejects `display: flex` and `display: grid` with domain exceptions.
- **Cryptographic Signatures**: The `SignedPdfCssPolicyDecorator` ensures that templates bypassing certain strict rules possess valid cryptographic signatures from internal designers.

## Installation

```bash
dotnet add package Muonroi.Pdf.Governance
```

## Quick Start

Governance is wired automatically when calling `builder.Services.AddMPdfService()` from the main package and then calling `AddPdfGovernance()`. However, you can interact with it directly to test whether a piece of HTML passes validation without invoking the full rendering engine.

```csharp
using Muonroi.Pdf.Governance.Policies;
using Muonroi.Pdf.Abstractions;

// 1. Initialize the policy. This requires the configuration object
// to check flags like AllowModernLayout.
PdfPolicySettings settings = new PdfPolicySettings { AllowModernLayout = false };
IPdfCssPolicy policy = new LegacyPrintPolicy(settings);

// 2. Mock a style declaration to evaluate
string cssProperty = "display";
string cssValue = "flex";

// 3. Evaluate
PolicyValidationResult result = policy.Evaluate(cssProperty, cssValue);

if (!result.IsValid)
{
    Console.WriteLine($"Violation! Rule {result.ViolatedRuleId}: {result.RejectionReason}");
    // Output: Violation! Rule POL-0012: Modern layout engine (flexbox/grid) is disabled by system policy.
}
```

## Layout Policy specifics

### `LegacyPrintPolicy` (Default)
This policy enables graceful degradation. For instance, if a user specifies an unknown `color` format, the policy will drop the property rather than rejecting the document outright.

**Modern Layout Handling**:
Flexbox and CSS Grid are intrinsically tied to `PdfPolicySettings.AllowModernLayout`. 
If `AllowModernLayout == false`, `LegacyPrintPolicy` will act as a strict gatekeeper:
- `display: flex` / `inline-flex` â†’ Rejected.
- `display: grid` / `inline-grid` â†’ Rejected.
- Sub-properties (`flex-direction`, `grid-template-columns`, `gap`, `justify-content`, etc.) â†’ Stripped entirely from the cascade.

If `AllowModernLayout == true`, these properties are marked as valid and passed downstream to the layout engine.

### `DefaultStrictPolicy`
An uncompromising policy designed for high-security multi-tenant SaaS environments. 
- Disallows all external resource fetching (`url(...)`).
- Completely forbids `position: absolute` or `position: fixed`.
- Flex/Grid are always blocked regardless of the `AllowModernLayout` setting.
Any violation results in an immediate `PdfPolicyException`, aborting the render.

## API Reference

### `AngleSharpHtmlParser`
Implements `IHtmlParser`. Converts a raw HTML string into an AST node that exposes a tree of elements.

### `AngleSharpCascadeEngine`
Implements `ICssCascadeEngine`. Responsible for resolving CSS specificity (matching class, id, tag selectors) and attaching a computed style dictionary to each node.

### `IPdfCssPolicy`
The core contract. Contains the `Evaluate(property, value)` method which returns a `PolicyValidationResult`.

### `SignedPdfCssPolicyDecorator`
Wraps another `IPdfCssPolicy`. It reads HMAC signatures embedded in HTML comments. If the signature is valid, it applies a more permissive sub-policy.

## Ecosystem Combinations

### + Muonroi.Pdf â†’ Pre-Render Security
When added together, `LegacyPrintPolicy` from this package automatically intercepts styling rules BEFORE `MPdfService` executes layout rendering, preventing malicious memory allocation attacks.

### + Muonroi.Tenancy.Core â†’ Tiered Security Profiles
Different tenants can be dynamically assigned different policy strictness (e.g., Free tenants restricted to `DefaultStrictPolicy` preventing remote images, while Premium tenants receive `LegacyPrintPolicy`).

### + Muonroi.Observability â†’ Policy Telemetry
Policy violation rates and rejected properties tracked automatically as OTel counters, helping identify broken user-supplied templates.

### Full PDF Production Stack
```csharp
builder.Services
    .AddMPdfService(config)             
    .AddPdfGovernance(config)           
    .AddPdfDesignSystem(config);        
```

## Samples
- [`Muonroi.Pdf.Samples`](../../samples/Muonroi.Pdf.Samples)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
