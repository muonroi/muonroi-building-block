# Muonroi.BuildingBlock Refactoring Plan
## From God Project to Modular Architecture

## 📊 Current State Analysis

### Critical Issues ⚠️

**Muonroi.BuildingBlock is a "God Project"**:
- 📁 **405 C# files** in a single project
- 📦 **156 NuGet dependencies** (!)
- 🗂️ **118 directories** mixing unrelated concerns
- 🔗 All features tightly coupled in one DLL

### Problems with Current Structure

1. **Violation of Single Responsibility Principle**
   - One project doing: Auth + Caching + Grpc + ORM + Messaging + Kubernetes + Rules + ...
   - Impossible to use just one feature without pulling entire dependency tree

2. **NuGet Dependency Hell**
   - Project using Dapper requires EF Core, Grpc, Kafka, RabbitMQ, Consul, etc.
   - Massive binary size (10+ MB for minimal features)

3. **Maintenance Nightmare**
   - 405 files make code navigation difficult
   - High risk of breaking changes affecting unrelated features
   - Difficult to test in isolation

4. **Poor Consumer Experience**
   ```csharp
   // Consumer just wants caching
   Install-Package Muonroi.BuildingBlock
   // But gets: Auth, Grpc, Kafka, RabbitMQ, Consul, Kubernetes, etc.
   ```

5. **Version Lock-in**
   - Can't update one feature without updating entire library
   - Breaking changes in Auth affect Caching users

---

## 🎯 Refactoring Goals

### Best Practices from Famous OSS Projects

**ASP.NET Core Model** (Microsoft):
```
Microsoft.AspNetCore.App (metapackage)
  ├── Microsoft.AspNetCore.Authentication.JwtBearer
  ├── Microsoft.AspNetCore.Caching.Redis
  ├── Microsoft.AspNetCore.Grpc
  └── ...each focused, independently versioned
```

**MassTransit Model**:
```
MassTransit (core abstractions)
  ├── MassTransit.RabbitMQ
  ├── MassTransit.Kafka
  └── MassTransit.Azure.ServiceBus
```

**Serilog Model** (extension pattern):
```
Serilog (core)
  ├── Serilog.Sinks.Console
  ├── Serilog.Sinks.Elasticsearch
  └── Serilog.Enrichers.Environment
```

### Key Principles

1. ✅ **Single Responsibility**: One package = one concern
2. ✅ **Dependency Inversion**: Core abstractions, optional implementations
3. ✅ **Versioning Independence**: Update packages independently
4. ✅ **Consumer Choice**: Install only what you need
5. ✅ **Extension Pattern**: Core + optional extensions

---

## 📦 Proposed Package Structure

### Core Layer (Required by all)

#### 1. **Muonroi.Core**
**Purpose**: Shared primitives and abstractions
**Size**: ~50 files
**Dependencies**: Microsoft.Extensions.* only

**Contents**:
- `External/SeedWorks/` → Base entities (Entity, AuditableEntity, IAggregateRoot)
- `External/Exceptions/` → Base exceptions
- `External/Models/BaseResponse.cs` → Response wrappers
- `External/Helper/StringHelper.cs` → Core utilities
- `External/JsonConverter/` → JSON converters
- `Contract/Interfaces/IUnitOfWork.cs` → Core abstractions

**Why separate**: Every other package needs these primitives

**Dependencies**:
```xml
<PackageReference Include="System.Text.Json" />
<PackageReference Include="Microsoft.Extensions.Primitives" />
```

---

#### 2. **Muonroi.Core.Abstractions**
**Purpose**: Interfaces and contracts
**Size**: ~30 files
**Dependencies**: Muonroi.Core only

**Contents**:
- `Contract/Interfaces/*` → All interfaces
- `External/Interfaces/*` → Service contracts

**Why separate**: Enables dependency inversion

---

### Multi-Tenancy Layer

#### 3. **Muonroi.Tenancy** ✅ (Already exists - keep separate)
**Purpose**: Multi-tenant infrastructure
**Current state**: Good separation

**Contents**:
- Tenant resolution strategies
- Tenant context management
- Tenant isolation filters

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core.Abstractions" />
<PackageReference Include="Microsoft.EntityFrameworkCore" />
```

---

### Authentication & Authorization Layer

#### 4. **Muonroi.Auth** ✅ (Already exists - enhance)
**Purpose**: Authentication (who you are)
**Size**: ~40 files

**Contents**:
- JWT token generation/validation
- Bearer token middleware
- OAuth/OIDC integration
- Password hashing (BCrypt)
- MFA support

**Move from BuildingBlock**:
- `External/BearerToken/*`
- `External/OAuth/*`
- `External/MAuthInfoContext.cs`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.OpenIdConnect" />
<PackageReference Include="BCrypt.Net-Core" />
```

---

#### 5. **Muonroi.AuthZ** ✅ (Already exists - enhance)
**Purpose**: Authorization (what you can do)
**Size**: ~30 files

**Contents**:
- Permission-based access control
- Role-based access control
- Policy-based authorization
- Permission tree/hierarchy

**Move from BuildingBlock**:
- `Internal/Services/PermissionService.cs` (1307 lines!)
- Permission-related filters

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="Muonroi.Auth" />
```

---

### Data Access Layer

#### 6. **Muonroi.Data.Abstractions**
**Purpose**: Repository/UnitOfWork abstractions
**Size**: ~15 files
**Dependencies**: Muonroi.Core only

**Contents**:
- `External/Repositories/IRepository.cs`
- `External/UnitOfWork/IUnitOfWork.cs`
- `External/Entity/` → Entity configurations

---

#### 7. **Muonroi.Data.EntityFrameworkCore**
**Purpose**: EF Core implementation
**Size**: ~40 files

**Move from BuildingBlock**:
- `External/ORMs/EFCore/*`
- `External/UnitOfWork/EFUnitOfWork.cs`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Data.Abstractions" />
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
```

**Extensions** (optional, separate packages):
- `Muonroi.Data.EntityFrameworkCore.SqlServer`
- `Muonroi.Data.EntityFrameworkCore.PostgreSQL`
- `Muonroi.Data.EntityFrameworkCore.MySQL`

---

#### 8. **Muonroi.Data.Dapper**
**Purpose**: Dapper micro-ORM support
**Size**: ~25 files

**Move from BuildingBlock**:
- `External/ORMs/Dapper/*`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Data.Abstractions" />
<PackageReference Include="Dapper" />
<PackageReference Include="Dapper.Extensions.NetCore" />
```

**Extensions**:
- `Muonroi.Data.Dapper.Caching.Redis`

---

### Caching Layer

#### 9. **Muonroi.Caching.Abstractions**
**Purpose**: Caching contracts
**Size**: ~10 files

**Contents**:
- `External/Caching/ICacheService.cs`
- Cache key strategies
- Invalidation patterns

---

#### 10. **Muonroi.Caching.Memory**
**Purpose**: In-memory caching
**Size**: ~8 files

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Caching.Abstractions" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" />
```

---

#### 11. **Muonroi.Caching.Redis**
**Purpose**: Distributed Redis caching
**Size**: ~15 files

**Move from BuildingBlock**:
- `External/Caching/RedisCacheService.cs`
- Distributed cache invalidation (Pub/Sub)

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Caching.Abstractions" />
<PackageReference Include="StackExchange.Redis" />
<PackageReference Include="FreeRedis" />
```

---

### Messaging & Events Layer

#### 12. **Muonroi.Messaging.Abstractions**
**Purpose**: Event/message contracts
**Size**: ~12 files

**Contents**:
- `External/Events/*` → Domain events
- `External/InternalEvents/*` → Integration events
- `External/Messaging/IMessageBus.cs`

---

#### 13. **Muonroi.Messaging.MassTransit**
**Purpose**: MassTransit integration
**Size**: ~20 files

**Move from BuildingBlock**:
- `External/Messaging/MassTransit/*`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Messaging.Abstractions" />
<PackageReference Include="MassTransit" />
```

**Extensions**:
- `Muonroi.Messaging.MassTransit.RabbitMQ`
- `Muonroi.Messaging.MassTransit.Kafka`

---

#### 14. **Muonroi.Mediator**
**Purpose**: CQRS/Mediator pattern (in-process)
**Size**: ~15 files

**Move from BuildingBlock**:
- `External/Mediator/*`
- `Internal/Behaviours/*` → Pipeline behaviors

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="MediatR" />
<PackageReference Include="FluentValidation" />
```

---

### Communication Layer

#### 15. **Muonroi.Grpc**
**Purpose**: gRPC services
**Size**: ~20 files

**Move from BuildingBlock**:
- `External/Grpc/*`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="Grpc.AspNetCore.Server" />
<PackageReference Include="Grpc.Net.Client" />
```

---

#### 16. **Muonroi.SignalR**
**Purpose**: Real-time communication
**Size**: ~15 files

**Move from BuildingBlock**:
- `External/SignalR/*`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" />
```

---

#### 17. **Muonroi.Bff** ✅ (Already exists - keep separate)
**Purpose**: Backend-for-Frontend pattern
**Current state**: Good separation

---

### Infrastructure Layer

#### 18. **Muonroi.Observability**
**Purpose**: OpenTelemetry, logging, metrics
**Size**: ~25 files

**Move from BuildingBlock**:
- `External/Observability/*`
- `External/Logging/*`
- OtelSetup.cs (linked file)

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="Serilog.AspNetCore" />
```

---

#### 19. **Muonroi.BackgroundJobs.Abstractions**
**Purpose**: Background job contracts
**Size**: ~8 files

**Contents**:
- `External/BackgroundJobs/IBackgroundJob.cs`

---

#### 20. **Muonroi.BackgroundJobs.Hangfire**
**Purpose**: Hangfire implementation
**Size**: ~12 files

**Dependencies**:
```xml
<PackageReference Include="Muonroi.BackgroundJobs.Abstractions" />
<PackageReference Include="Hangfire.Core" />
<PackageReference Include="Hangfire.AspNetCore" />
```

---

#### 21. **Muonroi.BackgroundJobs.Quartz**
**Purpose**: Quartz.NET implementation
**Size**: ~12 files

**Dependencies**:
```xml
<PackageReference Include="Muonroi.BackgroundJobs.Abstractions" />
<PackageReference Include="Quartz" />
<PackageReference Include="Quartz.Extensions.Hosting" />
```

---

### Service Discovery & Configuration

#### 22. **Muonroi.ServiceDiscovery.Consul**
**Purpose**: Consul integration
**Size**: ~15 files

**Move from BuildingBlock**:
- `External/Consul/*`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="Consul" />
```

---

#### 23. **Muonroi.Kubernetes**
**Purpose**: Kubernetes utilities
**Size**: ~12 files

**Move from BuildingBlock**:
- `External/Kubernetes/*`

---

### Resiliency & HTTP

#### 24. **Muonroi.Resilience**
**Purpose**: Polly integration, retry policies
**Size**: ~10 files

**Move from BuildingBlock**:
- `External/Polly/*`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="Polly" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" />
```

---

### Web & API Layer

#### 25. **Muonroi.AspNetCore**
**Purpose**: ASP.NET Core common features
**Size**: ~50 files

**Move from BuildingBlock**:
- `External/Controller/MGenericController.cs`
- `External/Middleware/*`
- `External/Filters/*`
- `External/Cors/*`
- `External/Response/*`

**Dependencies**:
```xml
<PackageReference Include="Muonroi.Core" />
<PackageReference Include="Microsoft.AspNetCore.Mvc" />
<PackageReference Include="FluentValidation.AspNetCore" />
```

---

#### 26. **Muonroi.AspNetCore.OpenApi**
**Purpose**: Swagger/OpenAPI support
**Size**: ~15 files

**Move from BuildingBlock**:
- Swagger configuration
- API versioning setup

**Dependencies**:
```xml
<PackageReference Include="Muonroi.AspNetCore" />
<PackageReference Include="Swashbuckle.AspNetCore" />
<PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" />
```

---

### Rule Engine (Already well-separated) ✅

- **Muonroi.RuleEngine.Abstractions** ✅
- **Muonroi.RuleEngine.Core** ✅
- **Muonroi.RuleEngine.NRules** ✅
- **Muonroi.RuleEngine.CEP** ✅
- **Muonroi.RuleEngine.DecisionTable** ✅
- **Muonroi.RuleEngine.DecisionTable.Web** ✅
- **Muonroi.RuleEngine.Testing** ✅
- **Muonroi.Rules** ✅ (FEEL implementation)

**Status**: Already follows best practices. No changes needed.

---

### Tooling (Already well-separated) ✅

- **Muonroi.RuleGen** ✅
- **Muonroi.DecisionTableGen** ✅

---

## 🎁 Metapackage for Convenience

#### **Muonroi.BuildingBlock.All**
**Purpose**: Convenience metapackage (empty, references all)
**For**: Quick prototyping, legacy compatibility

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Muonroi.Core" />
    <PackageReference Include="Muonroi.Auth" />
    <PackageReference Include="Muonroi.AuthZ" />
    <PackageReference Include="Muonroi.Tenancy" />
    <PackageReference Include="Muonroi.Data.EntityFrameworkCore" />
    <PackageReference Include="Muonroi.Caching.Redis" />
    <!-- ... all packages ... -->
  </ItemGroup>
</Project>
```

**Note**: Consumers should migrate to specific packages for production.

---

## 📋 Migration Strategy

### Phase 1: Core Foundation (Week 1-2)
1. Create `Muonroi.Core` with shared primitives
2. Create `Muonroi.Core.Abstractions` with interfaces
3. Update all existing packages to reference Core
4. ✅ No breaking changes yet

### Phase 2: Data Access (Week 3-4)
1. Extract `Muonroi.Data.Abstractions`
2. Split `Muonroi.Data.EntityFrameworkCore` and `Muonroi.Data.Dapper`
3. Remove ORM code from BuildingBlock
4. ⚠️ Breaking: Apps using EF Core need explicit package

### Phase 3: Caching & Messaging (Week 5-6)
1. Extract `Muonroi.Caching.*` packages
2. Extract `Muonroi.Messaging.*` packages
3. Extract `Muonroi.Mediator`
4. ⚠️ Breaking: Apps using caching/messaging need explicit packages

### Phase 4: Communication & Web (Week 7-8)
1. Extract `Muonroi.Grpc`, `Muonroi.SignalR`
2. Extract `Muonroi.AspNetCore` and `Muonroi.AspNetCore.OpenApi`
3. ⚠️ Breaking: Apps need explicit API packages

### Phase 5: Infrastructure (Week 9-10)
1. Extract `Muonroi.Observability`
2. Extract `Muonroi.BackgroundJobs.*`
3. Extract `Muonroi.ServiceDiscovery.Consul`, `Muonroi.Kubernetes`
4. Extract `Muonroi.Resilience`

### Phase 6: Cleanup & Deprecation (Week 11-12)
1. Mark old `Muonroi.BuildingBlock` as deprecated
2. Redirect to `Muonroi.BuildingBlock.All` metapackage
3. Create migration guide
4. Update all documentation

---

## 📊 Before & After Comparison

### Dependency Graph - Before
```
Consumer App
  └── Muonroi.BuildingBlock (405 files, 156 dependencies)
        Pulls in EVERYTHING (Auth, Cache, Grpc, Kafka, Consul, etc.)
```

### Dependency Graph - After
```
Consumer App (Microservice)
  ├── Muonroi.Core (lightweight primitives)
  ├── Muonroi.Auth (JWT only)
  ├── Muonroi.Data.EntityFrameworkCore.PostgreSQL (just Postgres)
  ├── Muonroi.Caching.Redis (just Redis)
  ├── Muonroi.Messaging.MassTransit.RabbitMQ (just RabbitMQ)
  └── Muonroi.AspNetCore (web features)

  Total: 6 focused packages instead of 1 monolith
```

---

## 🎯 Success Criteria

1. ✅ No single package > 100 files
2. ✅ Clear separation of concerns
3. ✅ Consumers install only what they need
4. ✅ Independent versioning per package
5. ✅ Total download size reduced by 70%+ for typical apps
6. ✅ Build time improved (parallel compilation)
7. ✅ Test isolation improved

---

## 📚 Comparison to Industry Leaders

### ASP.NET Core
**Before**: System.Web (monolithic)
**After**: 100+ focused packages

### MassTransit
**Pattern**: Core + transport packages
**Our adoption**: `Muonroi.Messaging.*` mirrors this

### Serilog
**Pattern**: Core + sinks + enrichers
**Our adoption**: `Muonroi.Caching.*`, `Muonroi.Data.*` mirrors this

### Polly
**Pattern**: Core + integrations
**Our adoption**: `Muonroi.Resilience` integrates Polly

---

## 🚀 Implementation Checklist

- [ ] Phase 1: Core Foundation
  - [ ] Create Muonroi.Core project
  - [ ] Create Muonroi.Core.Abstractions project
  - [ ] Move shared primitives
  - [ ] Update existing packages to reference Core

- [ ] Phase 2: Data Access
  - [ ] Create Muonroi.Data.Abstractions
  - [ ] Create Muonroi.Data.EntityFrameworkCore
  - [ ] Create Muonroi.Data.Dapper
  - [ ] Create database-specific extensions

- [ ] Phase 3: Caching & Messaging
  - [ ] Create Muonroi.Caching.* packages
  - [ ] Create Muonroi.Messaging.* packages
  - [ ] Create Muonroi.Mediator

- [ ] Phase 4: Communication & Web
  - [ ] Create Muonroi.Grpc
  - [ ] Create Muonroi.SignalR
  - [ ] Create Muonroi.AspNetCore
  - [ ] Create Muonroi.AspNetCore.OpenApi

- [ ] Phase 5: Infrastructure
  - [ ] Create Muonroi.Observability
  - [ ] Create Muonroi.BackgroundJobs.*
  - [ ] Create Muonroi.ServiceDiscovery.Consul
  - [ ] Create Muonroi.Kubernetes
  - [ ] Create Muonroi.Resilience

- [ ] Phase 6: Cleanup
  - [ ] Create Muonroi.BuildingBlock.All metapackage
  - [ ] Deprecate old Muonroi.BuildingBlock
  - [ ] Write migration guide
  - [ ] Update documentation
  - [ ] Update samples and templates

---

## 📝 Migration Guide for Consumers

### Before (Monolithic)
```csharp
// Single package for everything
Install-Package Muonroi.BuildingBlock

// Gets 156 dependencies, 10+ MB DLL
```

### After (Modular)
```csharp
// Install only what you need
Install-Package Muonroi.Core
Install-Package Muonroi.Auth
Install-Package Muonroi.Data.EntityFrameworkCore.PostgreSQL
Install-Package Muonroi.Caching.Redis
Install-Package Muonroi.AspNetCore

// Gets ~30 dependencies, ~3 MB total
```

### Example: API with Auth + EF Core + Redis
```csharp
// Before
Install-Package Muonroi.BuildingBlock
// 156 dependencies including unused: Grpc, Kafka, Consul, Dapper, MySQL, etc.

// After
Install-Package Muonroi.Core
Install-Package Muonroi.Auth
Install-Package Muonroi.Data.EntityFrameworkCore.PostgreSQL
Install-Package Muonroi.Caching.Redis
Install-Package Muonroi.AspNetCore
// 35 dependencies, only what's needed
```

---

## 🎉 Expected Benefits

1. **Developer Experience**
   - Clear package names indicate purpose
   - Faster NuGet restore (fewer packages)
   - Easier to understand dependency tree

2. **Performance**
   - Smaller binary size (70% reduction typical)
   - Faster app startup (fewer assemblies to load)
   - Better tree-shaking for self-contained deployments

3. **Maintainability**
   - Update packages independently
   - Breaking changes isolated to specific packages
   - Easier to test each package in isolation

4. **Versioning**
   - Semantic versioning per package
   - Auth can be v2.0 while Caching is v1.5
   - No version lock-in

5. **Community**
   - Contributors can focus on specific areas
   - Easier code reviews (smaller PRs)
   - Clear ownership per package

---

## 🔗 References

- [ASP.NET Core Package Structure](https://github.com/dotnet/aspnetcore)
- [MassTransit Package Strategy](https://github.com/MassTransit/MassTransit)
- [Serilog Extension Pattern](https://github.com/serilog/serilog)
- [Polly Package Design](https://github.com/App-vNext/Polly)
- [.NET Package Authoring Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/)

---

**Next Steps**: Review and approve this plan before proceeding with Phase 1 implementation.
