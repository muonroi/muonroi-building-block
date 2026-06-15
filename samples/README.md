# Muonroi Samples

Each **Quickstart** is a minimal `Microsoft.NET.Sdk.Web` API (net8.0) that wires up one
building-block package and exposes endpoints + Swagger demonstrating its primary public API.
**Scenario** samples combine multiple packages into a realistic end-to-end app.

Run any sample with `dotnet run` from its `src/<Name>.Api` folder, then open `/swagger`.

## Quickstart samples (one per feature package)

### Web / API host
- [Quickstart.AspNetCore](./Quickstart.AspNetCore): `AddBaseApi()` — API versioning, Swagger, health checks.
- [Quickstart.OpenApi](./Quickstart.OpenApi): Swagger operation filters (`MErrorResponseFilter`, `SwaggerDefaultValues`).
- [Quickstart.AspNetCore.RuleEngine](./Quickstart.AspNetCore.RuleEngine): rule-engine infrastructure wiring + `IRuleChangeStore` (license-gated).
- [Quickstart.Bff](./Quickstart.Bff): cookie auth + antiforgery and server-side `ITokenStore`.
- [Quickstart.Grpc](./Quickstart.Grpc): `AddGrpcServer`/`UseGrpcTransport` + `BaseGrpcService` client calls (license-gated).
- [Quickstart.Http](./Quickstart.Http): `BaseApiService` + correlation/auth `DelegatingHandler`s.
- [Quickstart.SignalR](./Quickstart.SignalR): `AddSignalRWithTenant`, hub mapping, schema notifier.

### Core / cross-cutting
- [Quickstart.Core](./Quickstart.Core): testable clock, JSON serializer, ambient execution context.
- [Quickstart.Logging](./Quickstart.Logging): structured logging via `IMLog<T>`, scoped properties, `IMLogFactory`.
- [Quickstart.Diagnostics](./Quickstart.Diagnostics): hierarchical runtime tracing — sessions, nested nodes, export.
- [Quickstart.Mapper](./Quickstart.Mapper): convention-based object mapping with `IMapFrom<T>` auto-registration.
- [Quickstart.Secrets](./Quickstart.Secrets): pluggable secret resolution via `ISecretProvider` over `IConfiguration`.
- [Quickstart.Services](./Quickstart.Services): generic EF Core CRUD via `MServiceBase` with overridable lifecycle hooks.

### Auth & governance
- [Quickstart.Auth](./Quickstart.Auth): RS256 JWT issue/validate/revoke, JWKS export, RSA key rotation, BCrypt hashing.
- [Quickstart.AuthZ](./Quickstart.AuthZ): rule-engine-backed authorization policy evaluation.
- [Quickstart.Governance](./Quickstart.Governance): `ILicenseGuard` tier/feature checks + license-info endpoint.
- [Quickstart.Governance.Enterprise](./Quickstart.Governance.Enterprise): enterprise governance + operations endpoints + SLO presets.

### Data & infrastructure
- [Quickstart.Data.Dapper](./Quickstart.Data.Dapper): tenant-aware Dapper command wrapper, type handlers, RLS bypass scope.
- [Quickstart.Data.EntityFrameworkCore](./Quickstart.Data.EntityFrameworkCore): `MDbContext` audit timestamping + soft-delete (in-memory).
- [Quickstart.Data.Events](./Quickstart.Data.Events): saga persistence (`MSagaDbContext`) + transactional outbox.
- [Quickstart.Tenancy](./Quickstart.Tenancy): tenant resolution middleware, AsyncLocal context, schema selection, connection mapping.
- [Quickstart.Kubernetes](./Quickstart.Kubernetes): binding `KubernetesConfigs` + `KubernetesClusterType`.
- [Quickstart.ServiceDiscovery](./Quickstart.ServiceDiscovery): Consul registration/activation that no-ops safely in Development.

### Rule engine (advanced)
- [Quickstart.RuleEngine.Runtime.Web](./Quickstart.RuleEngine.Runtime.Web): runtime ruleset governance Web API over a file store.
- [Quickstart.RuleEngine.EntityFrameworkCore](./Quickstart.RuleEngine.EntityFrameworkCore): Postgres-backed rule store + approval/canary workflow.
- [Quickstart.RuleEngine.NRules](./Quickstart.RuleEngine.NRules): NRules engine + `[Rule]` attribute (legacy/frozen package).
- [Quickstart.RuleEngine.Proliferation](./Quickstart.RuleEngine.Proliferation): proliferation engine + Postgres persistence + connector routing.

### Integration, docs & UI
- [Quickstart.Integration.Persistence](./Quickstart.Integration.Persistence): connector config + encrypted credential stores.
- [Quickstart.Pdf.Advanced](./Quickstart.Pdf.Advanced): PDF design-system templates + enterprise quality/feature-gate toolkit.
- [Quickstart.UiEngine.Catalog](./Quickstart.UiEngine.Catalog): UI engine connector catalog scanning + snapshot store.

### Previously added Quickstarts
- [Quickstart.BackgroundJobs](./Quickstart.BackgroundJobs): Hangfire + Quartz job scheduling.
- [Quickstart.Caching](./Quickstart.Caching): multi-level caching (`IMultiLevelCacheService`) over memory/Redis.
- [Quickstart.Mediator](./Quickstart.Mediator): commands/queries/notifications, pipeline behaviors, validators.
- [Quickstart.Messaging](./Quickstart.Messaging): MassTransit publish/consume.
- [Quickstart.Observability](./Quickstart.Observability): metrics/tracing/logging instrumentation.
- [Quickstart.Resilience](./Quickstart.Resilience): retry/circuit-breaker/timeout pipelines.
- [Quickstart.Integration](./Quickstart.Integration): integration connectors.
- [Quickstart.CEP](./Quickstart.CEP): complex event processing windows.
- [Quickstart.RuleEngine](./Quickstart.RuleEngine): minimal rule engine API with one business rule and orchestrator facts.
- [Quickstart.DecisionTable](./Quickstart.DecisionTable): minimal decision-table web host with Postgres-backed storage.

## Scenario samples
- [LoanApproval](./LoanApproval/README.md): rule engine + decision table artifacts + loan decision API.
- [MultiTenantSaaS](./MultiTenantSaaS/README.md): tenant-specific pricing rules + optional control-plane wiring.
- [FraudDetection](./FraudDetection/README.md): CEP-backed fraud alert API with tenant-isolated config and window state.
- [RuleSourceGen](./RuleSourceGen/README.md): source-generated discount rules with runtime discovery and spy-based tests.
- [Muonroi.Experience.Sample](./Muonroi.Experience.Sample): experience-extraction runtime.
- [Muonroi.Pdf.AotSample](./Muonroi.Pdf.AotSample): AOT-friendly PDF rendering.
- [TestProject.Service](./TestProject.Service) / [TestProject.Aggregate](./TestProject.Aggregate): multi-site `SiteProfile` tenancy hosts.
