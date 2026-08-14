# Muonroi.BackgroundJobs.Abstractions
> Core primitives for Muonroi.BackgroundJobs.Abstractions in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BackgroundJobs.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.BackgroundJobs.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.BackgroundJobs.Abstractions defines the core scheduling interfaces and configuration models for background processing. Central components like `IBackgroundJobScheduler` and `TenantAwareJobBase` ensure cross-provider compatibility and multi-tenant job execution.

## Features

- **Unified Scheduling**: Standardize job dispatching with `IBackgroundJobScheduler`.
- **Multi-Tenancy**: Automatically preserve tenant context during asynchronous execution via `TenantAwareJobBase`.
- **Job Configuration**: Define generic job behaviors using `BackgroundJobConfigs` and `JobType`.

## Quick Start

```csharp
using Muonroi.BackgroundJobs.Abstractions;

public class MyJob : TenantAwareJobBase { }
```

## Installation

```bash
dotnet add package Muonroi.BackgroundJobs.Abstractions
```

## Ecosystem Combinations

Combine with `Muonroi.BackgroundJobs.Hangfire` or `Muonroi.BackgroundJobs.Quartz` to instantly swap out the underlying execution engine without altering any of your application logic relying on `IBackgroundJobScheduler`.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.BackgroundJobs.Abstractions components.
