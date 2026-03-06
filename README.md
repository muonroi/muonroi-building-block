# Muonroi Building Block

Muonroi Building Block is a modular .NET infrastructure framework for monolith, modular monolith, and microservices architectures.

[![CI](https://github.com/muonroi/MuonroiBuildingBlock/actions/workflows/ci.yml/badge.svg)](https://github.com/muonroi/MuonroiBuildingBlock/actions/workflows/ci.yml)
[![OSS License](https://img.shields.io/badge/OSS-Apache%202.0-green.svg)](LICENSE-APACHE)
[![Commercial License](https://img.shields.io/badge/Enterprise-Commercial-blue.svg)](LICENSE-COMMERCIAL)

## Package System

| Package | Description | Tier |
| :--- | :--- | :---: |
| `Muonroi.Core.Abstractions` | Core contracts, interfaces, and base types. | OSS (free) |
| `Muonroi.Core` | Core services implementation (datetime, JSON, logging wrappers, execution context). | OSS (free) |
| `Muonroi.Governance.Abstractions` | License governance contracts and policy interfaces. | OSS (free) |
| `Muonroi.Governance` | OSS license governance implementation. | OSS (free) |
| `Muonroi.Governance.Enterprise` | Enterprise anti-tampering, audit chain, fail-closed policy controls. | Commercial |
| `Muonroi.Tenancy.Abstractions` | Multi-tenancy contracts and shared models. | OSS (free) |
| `Muonroi.Tenancy.Core` | Shared-database multi-tenancy core and tenant filters. | OSS (free) |
| `Muonroi.Tenancy` | Tenant runtime context and middleware integration. | OSS (free) |
| `Muonroi.RuleEngine.Abstractions` | Rule engine contracts. | OSS (free) |
| `Muonroi.RuleEngine.Core` | Rule engine execution core. | OSS (free) |
| `Muonroi.RuleEngine.SourceGenerators` | Source generators for rule authoring and diagnostics. | OSS (free) |
| `Muonroi.RuleEngine.Testing` | Testing helpers for rule orchestration. | OSS (free) |
| `Muonroi.RuleEngine.DecisionTable` | Decision table models, validation, conversion and persistence abstractions. | OSS (free) |
| `Muonroi.RuleEngine.NRules` | NRules integration for Muonroi Rule Engine. | OSS (free) |
| `Muonroi.RuleEngine.CEP` | Complex Event Processing integration. | OSS (free) |
| `Muonroi.RuleEngine.Runtime.Web` | Runtime web APIs and enterprise runtime integration surfaces. | Commercial |
| `Muonroi.RuleEngine.DecisionTable.Web` | Decision table web/API runtime package. | Commercial |
| `Muonroi.Data.Abstractions` | Data contracts and repository abstractions. | OSS (free) |
| `Muonroi.Data.Dapper` | Dapper integration for read-heavy data access. | OSS (free) |
| `Muonroi.Data.EntityFrameworkCore` | EF Core infrastructure and repository base. | OSS (free) |
| `Muonroi.Caching.Abstractions` | Caching contracts and cache configs. | OSS (free) |
| `Muonroi.Caching.Memory` | Multi-level memory/distributed cache implementation. | OSS (free) |
| `Muonroi.Caching.Redis` | Redis-backed caching integration. | Commercial |
| `Muonroi.Auth` | JWT auth infrastructure and middleware integrations. | OSS (free) |
| `Muonroi.AuthZ` | Advanced authorization package. | Commercial |
| `Muonroi.AspNetCore` | ASP.NET Core hosting integration and infrastructure extensions. | OSS (free) |
| `Muonroi.AspNetCore.OpenApi` | OpenAPI/Swagger integration. | OSS (free) |
| `Muonroi.Http` | HTTP client utilities and tenant propagation helpers. | OSS (free) |
| `Muonroi.Resilience` | Retry/circuit breaker/timeout policies with telemetry hooks. | OSS (free) |
| `Muonroi.Mapper` | Object mapping infrastructure. | OSS (free) |
| `Muonroi.Mediator` | Mediator pattern implementation and pipeline behaviors. | OSS (free) |
| `Muonroi.Messaging.Abstractions` | Messaging contracts and integration event abstractions. | OSS (free) |
| `Muonroi.Messaging.MassTransit` | MassTransit transport integration package. | Commercial |
| `Muonroi.Observability` | OpenTelemetry integration and instrumentation helpers. | OSS (free) |
| `Muonroi.BackgroundJobs.Abstractions` | Background job contracts and scheduler abstractions. | OSS (free) |
| `Muonroi.BackgroundJobs.Hangfire` | Hangfire scheduler integration package. | Commercial |
| `Muonroi.BackgroundJobs.Quartz` | Quartz scheduler integration package. | Commercial |
| `Muonroi.SignalR` | SignalR integration package. | Commercial |
| `Muonroi.Grpc` | gRPC integration package. | Commercial |
| `Muonroi.Secrets` | Secret management integrations. | Commercial |
| `Muonroi.Bff` | Backend-for-Frontend package. | Commercial |
| `Muonroi.ServiceDiscovery.Consul` | Consul service discovery integration. | Commercial |
| `Muonroi.UiEngine.Catalog` | UI engine catalog package. | Commercial |
| `Muonroi.BuildingBlock.Shared` | Shared result/pagination/common utility types. | OSS (free) |
| `Muonroi.Logging` | Structured logging implementation. | OSS (free) |
| `Muonroi.Logging.Abstractions` | Logging contracts and context interfaces. | OSS (free) |
| `Muonroi.Rules` | Rule definitions and conventions package. | OSS (free) |
| `Muonroi.BuildingBlock.All` | Meta-package aggregating OSS and commercial modules. | Commercial |

## Quick Install

```bash
# OSS packages (public NuGet.org)
dotnet add package Muonroi.Core
dotnet add package Muonroi.RuleEngine.Core

# Commercial packages (private feed - requires license)
# See: https://muonroi.com/docs/commercial/setup
```

## Documentation

- Docs site: https://muonroi.github.io/MuonroiBuildingBlock/
- Commercial editions: [COMMERCIAL-EDITIONS.md](COMMERCIAL-EDITIONS.md)
- OSS/commercial boundary: [OSS-BOUNDARY.md](OSS-BOUNDARY.md)

## License

This repository uses dual licensing:

- OSS packages are licensed under Apache License 2.0 (`LICENSE-APACHE`).
- Enterprise/commercial packages are licensed under Muonroi Commercial License (`LICENSE-COMMERCIAL`).
