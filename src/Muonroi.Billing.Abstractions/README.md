# Muonroi.Billing.Abstractions
> Core primitives for Muonroi.Billing.Abstractions in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Billing.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Billing.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.Billing.Abstractions defines the foundational interfaces and models for tracking multi-tenant resource usage and metering. It includes the `BillableEvent` record, `IUsageAggregator` for batching, and `IBillingProvider` for standardizing provider interactions.

## Features

- **Billable Events**: Emit strongly typed `BillableEvent` records from anywhere in the system.
- **Usage Aggregation**: Use `IUsageAggregator` to batch high-volume events in memory before flushing them to the data store.
- **Provider Agnostic**: The `IBillingProvider` interface standardizes operations like syncing customer usage.

## Quick Start

```csharp
using Muonroi.Billing.Abstractions;

builder.Services.AddBillingAbstractions();
```

## Installation

```bash
dotnet add package Muonroi.Billing.Abstractions
```

## Ecosystem Combinations

Combine with `Muonroi.BackgroundJobs.Abstractions` to periodically flush locally aggregated `BillableEvent` records using a scheduled job, minimizing the performance impact of metering high-volume API requests.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.Billing.Abstractions components.
