# Muonroi.BackgroundJobs.Quartz
> Core primitives for Muonroi.BackgroundJobs.Quartz in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BackgroundJobs.Quartz.svg)](https://www.nuget.org/packages/Muonroi.BackgroundJobs.Quartz/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.BackgroundJobs.Quartz is the Quartz.NET-backed implementation of the `Muonroi.BackgroundJobs.Abstractions` interfaces. It leverages the scheduling capabilities of Quartz via `QuartzJobScheduler` and maintains tenant boundaries using `QuartzContextJobListener`.

## Features

- **Seamless Abstraction**: Implements `IBackgroundJobScheduler` via `QuartzJobScheduler`.
- **Tenant Context Preservation**: Utilizes `QuartzContextJobListener` to manage the serialization and deserialization of the tenant context between threads.
- **Quartz Configuration**: Manage scheduler behavior via `QuartzJobOptions`.

## Quick Start

```csharp
using Muonroi.BackgroundJobs.Quartz;

builder.Services.AddQuartzJobScheduler();
```

## Installation

```bash
dotnet add package Muonroi.BackgroundJobs.Quartz
```

## Ecosystem Combinations

Combine with `Muonroi.BackgroundJobs.Abstractions` to bring high-performance, cron-heavy in-process scheduling to your application while maintaining strict tenant isolation.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.BackgroundJobs.Quartz components.
