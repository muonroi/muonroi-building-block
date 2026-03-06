# File Migration Mapping Guide
## From Muonroi.BuildingBlock to 40 Packages

**Purpose**: Detailed file-by-file mapping for migration

---

## 📦 Muonroi.Core (Target: ~30 files)

### SeedWorks/
```
SOURCE: BuildingBlock/External/SeedWorks/
TARGET: Core/SeedWorks/

Entity.cs                    → Entity.cs
AuditableEntity.cs           → AuditableEntity.cs
IAggregateRoot.cs            → IAggregateRoot.cs
ValueObject.cs               → ValueObject.cs
Enumeration.cs               → Enumeration.cs
```

### Models/
```
SOURCE: BuildingBlock/External/Models/
TARGET: Core/Models/

BaseResponse.cs              → BaseResponse.cs
PaginatedList.cs             → PaginatedList.cs (rename from MPagedList.cs)
ErrorDetails.cs              → ErrorDetails.cs
```

### Exceptions/
```
SOURCE: BuildingBlock/External/Exceptions/
TARGET: Core/Exceptions/

DomainException.cs           → DomainException.cs
NotFoundException.cs         → NotFoundException.cs
ValidationException.cs       → ValidationException.cs
UnauthorizedException.cs     → UnauthorizedException.cs
ConflictException.cs         → ConflictException.cs
```

### Extensions/
```
SOURCE: BuildingBlock/External/Helper/, External/Extensions/
TARGET: Core/Extensions/

StringHelper.cs              → StringExtensions.cs (rename + refactor)
DateTimeHelper.cs            → DateTimeExtensions.cs (rename)
EnumerableExtensions.cs      → EnumerableExtensions.cs
ObjectExtensions.cs          → ObjectExtensions.cs
```

### Utilities/
```
SOURCE: BuildingBlock/External/Helper/
TARGET: Core/Utilities/

Guard.cs                     → Guard.cs (NEW - implement guard clauses)
Clock.cs                     → Clock.cs (NEW - time abstraction)
GuidGenerator.cs             → GuidGenerator.cs (NEW - ID generation)
```

---

## 📦 Muonroi.Core.Abstractions (Target: ~15 files)

### Repositories/
```
SOURCE: BuildingBlock/Contract/Interfaces/
TARGET: Core.Abstractions/Repositories/

IRepository.cs               → IRepository.cs
IReadRepository.cs           → IReadRepository.cs (NEW - split from IRepository)
```

### Specifications/
```
SOURCE: BuildingBlock/External/ (may not exist, create new)
TARGET: Core.Abstractions/Specifications/

ISpecification.cs            → ISpecification.cs (NEW)
Specification.cs             → Specification.cs (NEW)
```

### Services/
```
SOURCE: BuildingBlock/External/Interfaces/
TARGET: Core.Abstractions/Services/

ICurrentUser.cs              → ICurrentUser.cs
IDateTime.cs                 → IDateTime.cs (NEW)
IEventPublisher.cs           → IEventPublisher.cs
```

---

## 📦 Muonroi.Data.Abstractions (Target: ~12 files)

```
SOURCE: BuildingBlock/External/Repositories/, Contract/Interfaces/
TARGET: Data.Abstractions/

IRepository.cs               → Repositories/IRepository.cs
IAsyncRepository.cs          → Repositories/IAsyncRepository.cs
IUnitOfWork.cs               → UnitOfWork/IUnitOfWork.cs
IUnitOfWorkFactory.cs        → UnitOfWork/IUnitOfWorkFactory.cs (NEW)
ISpecification.cs            → Specifications/ISpecification.cs
PagedQuery.cs                → Queries/PagedQuery.cs
```

---

## 📦 Muonroi.Data.EntityFrameworkCore (Target: ~35 files)

```
SOURCE: BuildingBlock/External/ORMs/EFCore/
TARGET: Data.EntityFrameworkCore/

EFRepository.cs              → Repositories/EFRepository.cs (rename from MEFRepository.cs)
EFReadRepository.cs          → Repositories/EFReadRepository.cs (NEW - read-only)
EFUnitOfWork.cs              → UnitOfWork/EFUnitOfWork.cs (rename from MEFUnitOfWork.cs)

ModelBuilderExtensions.cs    → Extensions/ModelBuilderExtensions.cs
QueryableExtensions.cs       → Extensions/QueryableExtensions.cs
DbContextExtensions.cs       → Extensions/DbContextExtensions.cs

AuditInterceptor.cs          → Interceptors/AuditInterceptor.cs (NEW)
SoftDeleteInterceptor.cs     → Interceptors/SoftDeleteInterceptor.cs (NEW)

EntityConfiguration.cs       → Configuration/EntityConfiguration.cs
```

---

## 📦 Muonroi.Data.Dapper (Target: ~20 files)

```
SOURCE: BuildingBlock/External/ORMs/Dapper/
TARGET: Data.Dapper/

DapperRepository.cs          → Repositories/DapperRepository.cs (rename from MDapperRepository.cs)
DapperReadRepository.cs      → Repositories/DapperReadRepository.cs
DapperUnitOfWork.cs          → UnitOfWork/DapperUnitOfWork.cs

DapperExtensions.cs          → Extensions/DapperExtensions.cs
SqlBuilderExtensions.cs      → Extensions/SqlBuilderExtensions.cs

JsonTypeHandler.cs           → TypeHandlers/JsonTypeHandler.cs
DateTimeOffsetHandler.cs     → TypeHandlers/DateTimeOffsetHandler.cs
```

---

## 📦 Muonroi.Caching.Abstractions (Target: ~8 files)

```
SOURCE: BuildingBlock/External/Caching/ (interfaces)
TARGET: Caching.Abstractions/

ICacheService.cs             → ICacheService.cs (extract interface)
ICacheKeyGenerator.cs        → ICacheKeyGenerator.cs (NEW)
CacheOptions.cs              → CacheOptions.cs (NEW)
CacheEntryOptions.cs         → CacheEntryOptions.cs (NEW)

IInvalidationStrategy.cs     → Strategies/IInvalidationStrategy.cs (NEW)
```

---

## 📦 Muonroi.Caching.Memory (Target: ~6 files)

```
SOURCE: BuildingBlock/External/Caching/
TARGET: Caching.Memory/

MemoryCacheService.cs        → MemoryCacheService.cs
MemoryCacheOptions.cs        → MemoryCacheOptions.cs
ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.Caching.Redis (Target: ~12 files)

```
SOURCE: BuildingBlock/External/Caching/
TARGET: Caching.Redis/

RedisCacheService.cs         → RedisCacheService.cs (rename from MRedisCacheService.cs)
RedisOptions.cs              → RedisOptions.cs
RedisConnection.cs           → RedisConnection.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs

RedisPubSubInvalidation.cs   → Invalidation/RedisPubSubInvalidation.cs
RedisInvalidationChannel.cs  → Invalidation/RedisInvalidationChannel.cs
```

---

## 📦 Muonroi.Messaging.Abstractions (Target: ~10 files)

```
SOURCE: BuildingBlock/External/Events/, External/InternalEvents/
TARGET: Messaging.Abstractions/

IMessageBus.cs               → IMessageBus.cs (NEW)
IEventPublisher.cs           → IEventPublisher.cs
IEventHandler.cs             → IEventHandler.cs

IEvent.cs                    → Events/IEvent.cs
IIntegrationEvent.cs         → Events/IIntegrationEvent.cs
IDomainEvent.cs              → Events/IDomainEvent.cs

IMessage.cs                  → Messages/IMessage.cs (NEW)
MessageMetadata.cs           → Messages/MessageMetadata.cs (NEW)
```

---

## 📦 Muonroi.Messaging.MassTransit (Target: ~15 files)

```
SOURCE: BuildingBlock/External/Messaging/
TARGET: Messaging.MassTransit/

MassTransitEventBus.cs       → MassTransitEventBus.cs
MassTransitOptions.cs        → MassTransitOptions.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
MassTransitConfiguration.cs  → Configuration/MassTransitConfiguration.cs

EventPublisher.cs            → Publishers/EventPublisher.cs
```

---

## 📦 Muonroi.Mediator (Target: ~12 files)

```
SOURCE: BuildingBlock/External/Mediator/, Internal/Behaviours/
TARGET: Mediator/

MediatorService.cs           → MediatorService.cs (rename from MMediatorService.cs)

ValidationBehaviour.cs       → Behaviours/ValidationBehaviour.cs
LoggingBehaviour.cs          → Behaviours/LoggingBehaviour.cs
PerformanceBehaviour.cs      → Behaviours/PerformanceBehaviour.cs
TransactionBehaviour.cs      → Behaviours/TransactionBehaviour.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.AspNetCore (Target: ~40 files)

```
SOURCE: BuildingBlock/External/Controller/, External/Middleware/, External/Filters/
TARGET: AspNetCore/

MGenericController.cs        → Controllers/GenericController.cs (rename, remove M)
ControllerExtensions.cs      → Controllers/ControllerExtensions.cs

ExceptionHandlingMiddleware.cs → Middleware/ExceptionHandlingMiddleware.cs
RequestLoggingMiddleware.cs  → Middleware/RequestLoggingMiddleware.cs
TenantResolutionMiddleware.cs → Middleware/TenantResolutionMiddleware.cs

ValidationFilter.cs          → Filters/ValidationFilter.cs
AuthorizationFilter.cs       → Filters/AuthorizationFilter.cs
AuditFilter.cs               → Filters/AuditFilter.cs

CustomModelBinders.cs        → ModelBinding/CustomModelBinders.cs
ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.AspNetCore.OpenApi (Target: ~10 files)

```
SOURCE: BuildingBlock/External/ (Swagger config)
TARGET: AspNetCore.OpenApi/

SwaggerConfiguration.cs      → SwaggerConfiguration.cs
SwaggerOptions.cs            → SwaggerOptions.cs
SecurityDefinitions.cs       → SecurityDefinitions.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
OperationFilters/           → OperationFilters/ (custom filters)
```

---

## 📦 Muonroi.Grpc (Target: ~15 files)

```
SOURCE: BuildingBlock/External/Grpc/
TARGET: Grpc/

GrpcServiceBase.cs           → Services/GrpcServiceBase.cs
GrpcClientFactory.cs         → Clients/GrpcClientFactory.cs
GrpcOptions.cs               → GrpcOptions.cs

GrpcExceptionInterceptor.cs  → Interceptors/GrpcExceptionInterceptor.cs
GrpcLoggingInterceptor.cs    → Interceptors/GrpcLoggingInterceptor.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.SignalR (Target: ~12 files)

```
SOURCE: BuildingBlock/External/SignalR/
TARGET: SignalR/

HubBase.cs                   → Hubs/HubBase.cs
SignalROptions.cs            → SignalROptions.cs

HubConnectionExtensions.cs   → Extensions/HubConnectionExtensions.cs
ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.Observability (Target: ~20 files)

```
SOURCE: BuildingBlock/External/Observability/, External/Logging/
TARGET: Observability/

OtelSetup.cs                 → OpenTelemetry/OtelSetup.cs
OtelOptions.cs               → OpenTelemetry/OtelOptions.cs

SerilogConfiguration.cs      → Logging/SerilogConfiguration.cs
SerilogEnrichers.cs          → Logging/SerilogEnrichers.cs

MetricsCollector.cs          → Metrics/MetricsCollector.cs
HealthCheckConfiguration.cs  → HealthChecks/HealthCheckConfiguration.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.BackgroundJobs.Abstractions (Target: ~6 files)

```
SOURCE: BuildingBlock/External/BackgroundJobs/ (interfaces)
TARGET: BackgroundJobs.Abstractions/

IBackgroundJob.cs            → IBackgroundJob.cs
IJobScheduler.cs             → IJobScheduler.cs (NEW)
JobOptions.cs                → JobOptions.cs (NEW)
```

---

## 📦 Muonroi.BackgroundJobs.Hangfire (Target: ~10 files)

```
SOURCE: BuildingBlock/External/BackgroundJobs/Hangfire/
TARGET: BackgroundJobs.Hangfire/

HangfireJobScheduler.cs      → HangfireJobScheduler.cs
HangfireOptions.cs           → HangfireOptions.cs
HangfireConfiguration.cs     → HangfireConfiguration.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.BackgroundJobs.Quartz (Target: ~10 files)

```
SOURCE: BuildingBlock/External/BackgroundJobs/Quartz/
TARGET: BackgroundJobs.Quartz/

QuartzJobScheduler.cs        → QuartzJobScheduler.cs
QuartzOptions.cs             → QuartzOptions.cs
QuartzConfiguration.cs       → QuartzConfiguration.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.ServiceDiscovery.Consul (Target: ~12 files)

```
SOURCE: BuildingBlock/External/Consul/
TARGET: ServiceDiscovery.Consul/

ConsulServiceRegistry.cs     → ConsulServiceRegistry.cs
ConsulOptions.cs             → ConsulOptions.cs
ConsulHealthCheck.cs         → ConsulHealthCheck.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.Kubernetes (Target: ~10 files)

```
SOURCE: BuildingBlock/External/Kubernetes/
TARGET: Kubernetes/

KubernetesOptions.cs         → KubernetesOptions.cs
KubernetesHealthCheck.cs     → KubernetesHealthCheck.cs
PodInfo.cs                   → Models/PodInfo.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.Resilience (Target: ~10 files)

```
SOURCE: BuildingBlock/External/Polly/
TARGET: Resilience/

ResiliencePolicies.cs        → Policies/ResiliencePolicies.cs
RetryPolicy.cs               → Policies/RetryPolicy.cs
CircuitBreakerPolicy.cs      → Policies/CircuitBreakerPolicy.cs
TimeoutPolicy.cs             → Policies/TimeoutPolicy.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.Governance (Target: ~8 files)

```
SOURCE: BuildingBlock/Internal/ (governance related)
TARGET: Governance/

PolicyEnforcement.cs         → PolicyEnforcement.cs (NEW)
ComplianceChecker.cs         → ComplianceChecker.cs (NEW)
AuditLogger.cs               → AuditLogger.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.Tenancy.Abstractions (Target: ~6 files)

```
SOURCE: Muonroi.Tenancy/ (extract interfaces)
TARGET: Tenancy.Abstractions/

ITenantResolver.cs           → ITenantResolver.cs
ITenantContext.cs            → ITenantContext.cs
ITenantStore.cs              → ITenantStore.cs
TenantInfo.cs                → Models/TenantInfo.cs
```

---

## 📦 Muonroi.Tenancy.Core (Target: ~10 files)

```
SOURCE: Muonroi.Tenancy/ (core logic)
TARGET: Tenancy.Core/

TenantResolver.cs            → TenantResolver.cs
TenantContext.cs             → TenantContext.cs
TenantMiddleware.cs          → Middleware/TenantMiddleware.cs

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs
```

---

## 📦 Muonroi.RuleEngine.Runtime (NEW - Target: ~8 files)

```
SOURCE: NEW (orchestration layer)
TARGET: RuleEngine.Runtime/

RuntimeOrchestrator.cs       → RuntimeOrchestrator.cs (NEW - combines Core + DecisionTable + NRules)
RuntimeOptions.cs            → RuntimeOptions.cs (NEW)
EngineSelector.cs            → EngineSelector.cs (NEW - selects Core vs NRules vs DecisionTable)

ServiceCollectionExtensions.cs → Extensions/ServiceCollectionExtensions.cs (NEW)
```

**Purpose**: Provides unified API to use any rule engine implementation

---

## 🔄 Namespace Changes

### Keep M* Prefix With Domain-Qualified Pattern

```
OLD Namespace: Muonroi.BuildingBlock.External.Controllers
NEW Namespace: Muonroi.AspNetCore.Controllers

OLD Class: MGenericController<T>
NEW Class: MWebGenericController<T>

OLD Class: MAuthInfoContext
NEW Class: MAuthContext

OLD Class: MEFRepository<T>
NEW Class: MDataEfRepository<T>

OLD Class: MRedisCacheService
NEW Class: MCacheRedisService
```

### Namespace Mapping

```
Muonroi.BuildingBlock.External.SeedWorks.*
  → Muonroi.Core.SeedWorks.*

Muonroi.BuildingBlock.External.ORMs.EFCore.*
  → Muonroi.Data.EntityFrameworkCore.*

Muonroi.BuildingBlock.External.Caching.*
  → Muonroi.Caching.Redis.*

Muonroi.BuildingBlock.External.Messaging.*
  → Muonroi.Messaging.MassTransit.*

Muonroi.BuildingBlock.External.Controller.*
  → Muonroi.AspNetCore.Controllers.*
```

---

## 🧪 Test Migration Mapping

```
SOURCE: tests/Muonroi.BuildingBlock.Test/
TARGET: tests/{Feature}.Tests/

BuildingBlock.Test/SeedWorks/EntityTests.cs
  → Core.Tests/SeedWorks/EntityTests.cs

BuildingBlock.Test/Repositories/EFRepositoryTests.cs
  → Data.EntityFrameworkCore.Tests/Repositories/EFRepositoryTests.cs

BuildingBlock.Test/Caching/RedisCacheTests.cs
  → Caching.Redis.Tests/RedisCacheServiceTests.cs

BuildingBlock.Test/Messaging/EventBusTests.cs
  → Messaging.MassTransit.Tests/EventBusTests.cs
```

---

## 📋 Verification Checklist

After migrating each file:

- [ ] File moved to correct package
- [ ] Namespace updated
- [ ] M* prefix removed (if applicable)
- [ ] Using statements updated
- [ ] Access modifiers correct (internal vs public)
- [ ] XML documentation added
- [ ] No references to old BuildingBlock package
- [ ] Tests migrated and passing
- [ ] Sample demonstrates usage

---

## 🔧 Automated Migration Script

```bash
#!/bin/bash
# migrate-file.sh <source-file> <target-package> <target-path>

SOURCE=$1
TARGET_PACKAGE=$2
TARGET_PATH=$3

# Copy file
cp "$SOURCE" "src/$TARGET_PACKAGE/$TARGET_PATH"

# Update namespace
sed -i "s/namespace Muonroi.BuildingBlock.*/namespace $TARGET_PACKAGE.${TARGET_PATH%/*}/g" \
  "src/$TARGET_PACKAGE/$TARGET_PATH"

# Remove M prefix from class names (careful!)
# sed -i 's/class M\([A-Z]\)/class \1/g' "src/$TARGET_PACKAGE/$TARGET_PATH"

echo "Migrated $SOURCE to $TARGET_PACKAGE/$TARGET_PATH"
```

**Usage**:
```bash
./migrate-file.sh \
  "src/Muonroi.BuildingBlock/External/SeedWorks/Entity.cs" \
  "Muonroi.Core" \
  "SeedWorks/Entity.cs"
```

---

**Next**: Use this mapping to systematically migrate all files from BuildingBlock to the 40 new packages.
