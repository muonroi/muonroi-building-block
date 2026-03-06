# OSS / Commercial Package Boundary

## Rule
- OSS packages MUST NOT depend on Commercial packages.
- Verified by: `scripts/check-modular-boundaries.ps1`

## OSS Packages (Apache 2.0 - public NuGet)
- Muonroi.Core.Abstractions
- Muonroi.Core
- Muonroi.Governance.Abstractions        (Phase 0.2 creates this)
- Muonroi.Governance
- Muonroi.Tenancy.Abstractions
- Muonroi.Tenancy.Core
- Muonroi.Tenancy
- Muonroi.RuleEngine.Abstractions
- Muonroi.RuleEngine.Core
- Muonroi.RuleEngine.SourceGenerators
- Muonroi.RuleEngine.Testing
- Muonroi.RuleEngine.DecisionTable
- Muonroi.RuleEngine.NRules
- Muonroi.RuleEngine.CEP
- Muonroi.Data.Abstractions
- Muonroi.Data.Dapper
- Muonroi.Data.EntityFrameworkCore
- Muonroi.Caching.Abstractions
- Muonroi.Caching.Memory
- Muonroi.Auth
- Muonroi.AspNetCore
- Muonroi.AspNetCore.OpenApi
- Muonroi.Http
- Muonroi.Resilience
- Muonroi.Mapper
- Muonroi.Mediator
- Muonroi.Messaging.Abstractions
- Muonroi.Observability
- Muonroi.BackgroundJobs.Abstractions
- Muonroi.BuildingBlock.Shared
- Muonroi.Logging
- Muonroi.Logging.Abstractions

## Commercial Packages (Muonroi Commercial License - private feed)
- Muonroi.Governance.Enterprise           (Phase 0.2 creates this)
- Muonroi.AuthZ
- Muonroi.Caching.Redis
- Muonroi.Messaging.MassTransit
- Muonroi.BackgroundJobs.Hangfire
- Muonroi.BackgroundJobs.Quartz
- Muonroi.SignalR
- Muonroi.Grpc
- Muonroi.Secrets
- Muonroi.Bff
- Muonroi.ServiceDiscovery.Consul
- Muonroi.RuleEngine.Runtime.Web
- Muonroi.RuleEngine.DecisionTable.Web
- Muonroi.UiEngine.Catalog
