# Muonroi.BackgroundJobs.Hangfire
> Core primitives for Muonroi.BackgroundJobs.Hangfire in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BackgroundJobs.Hangfire.svg)](https://www.nuget.org/packages/Muonroi.BackgroundJobs.Hangfire/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.BackgroundJobs.Hangfire is the Hangfire-backed implementation of the `Muonroi.BackgroundJobs.Abstractions` interfaces. It implements `HangfireJobScheduler` to execute jobs and `JobContextActivatorFilter` to manage multi-tenant scope.

## Features

- **Seamless Abstraction**: Implements `IBackgroundJobScheduler` via `HangfireJobScheduler`.
- **Tenant Context Preservation**: Utilizes `JobContextActivatorFilter` to serialize tenant and correlation IDs during enqueuing and deserialize them prior to execution.
- **Easy Registration**: Configured easily via `HangfireProviderRegistration`.

## Quick Start

```csharp
using Muonroi.BackgroundJobs.Hangfire;

builder.Services.AddHangfireJobScheduler();
```

## Installation

```bash
dotnet add package Muonroi.BackgroundJobs.Hangfire
```

## Ecosystem Combinations

Combine with `Muonroi.BackgroundJobs.Abstractions` to execute multi-tenant aware background jobs seamlessly without polluting your business logic with Hangfire-specific attributes.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.BackgroundJobs.Hangfire components.
