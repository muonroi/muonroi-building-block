# Muonroi.AspNetCore
> Core primitives for Muonroi.AspNetCore in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AspNetCore.svg)](https://www.nuget.org/packages/Muonroi.AspNetCore/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.AspNetCore provides core ASP.NET Core integrations for the ecosystem. It includes foundational controllers like `MControllerBase`, security attributes such as `AuthorizePermissionAttribute`, and essential middleware like `QuotaEnforcementMiddleware` and `JwtMiddleware`.

## Features

- **Controllers & Attributes**: Base implementations via `MControllerBase` and declarative security using `AuthorizePermissionAttribute` and `GenericCrudPermissionAttribute`.
- **Middleware**: Built-in request pipeline components such as `QuotaEnforcementMiddleware`, `JwtMiddleware`, and `MCookieAuthMiddleware`.
- **Diagnostic Filters**: Telemetry and state filters including `FeatureFlagFilter` and `RequestLoggingFilter`.
- **Extensions**: Registration helpers via `ServiceCollectionExtensions`.

## Quick Start

```csharp
using Muonroi.AspNetCore;

builder.Services.AddMuonroiAspNetCore();
```

## Installation

```bash
dotnet add package Muonroi.AspNetCore
```

## Ecosystem Combinations

Combine with `Muonroi.Auth` to enable complete token validation and claims transformation natively within the ASP.NET Core pipeline, allowing controllers inheriting from `MControllerBase` to automatically enforce multi-tenant quotas.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.AspNetCore components.
