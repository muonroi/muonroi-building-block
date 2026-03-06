# Muonroi Package Dependency Graph

## 🎯 Proposed Architecture (After Refactoring)

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Application Layer                           │
│                    (Consumer Microservices/APIs)                    │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             │ Consumes (Pick what you need)
                             ↓
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│  ╔═══════════════════════════════════════════════════════════════╗ │
│  ║                    🎁 Metapackage Layer                       ║ │
│  ╠═══════════════════════════════════════════════════════════════╣ │
│  ║  Muonroi.BuildingBlock.All                                    ║ │
│  ║  (Empty package, references all below for backward compat)    ║ │
│  ╚═══════════════════════════════════════════════════════════════╝ │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────────────┐
│                     Feature Packages Layer                          │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────┬─────────────┬─────────────┬─────────────┬─────────────┐
│ 🔒 Auth     │ 💾 Data     │ 🗄️ Caching  │ 📨 Messaging│ 🌐 Comm     │
├─────────────┼─────────────┼─────────────┼─────────────┼─────────────┤
│ Muonroi     │ Muonroi     │ Muonroi     │ Muonroi     │ Muonroi     │
│ .Auth       │ .Data       │ .Caching    │ .Messaging  │ .Grpc       │
│   ↓         │ .EFCore     │ .Redis      │ .MassTransit│   ↓         │
│ Muonroi     │   ↓         │   ↓         │   ↓         │ Muonroi     │
│ .AuthZ      │ Muonroi     │ Muonroi     │ Muonroi     │ .SignalR    │
│             │ .Data       │ .Caching    │ .Mediator   │   ↓         │
│             │ .Dapper     │ .Memory     │             │ Muonroi     │
│             │             │             │             │ .Bff        │
└─────────────┴─────────────┴─────────────┴─────────────┴─────────────┘

┌─────────────┬─────────────┬─────────────┬─────────────┬─────────────┐
│ 🏗️ Infra    │ 🔄 Resil    │ 🌐 Web      │ 🏢 Tenant   │ 🧠 Rules    │
├─────────────┼─────────────┼─────────────┼─────────────┼─────────────┤
│ Muonroi     │ Muonroi     │ Muonroi     │ Muonroi     │ Already     │
│ .Observ     │ .Resilience │ .AspNetCore │ .Tenancy    │ well-       │
│   ↓         │             │   ↓         │   ↓         │ separated   │
│ Muonroi     │             │ Muonroi     │             │ ✅          │
│ .BackJobs   │             │ .AspNetCore │             │             │
│ .Hangfire   │             │ .OpenApi    │ Muonroi     │             │
│   ↓         │             │             │ .Tenancy    │             │
│ Muonroi     │             │             │ .Core       │             │
│ .BackJobs   │             │             │   ↓         │             │
│ .Quartz     │             │             │ Muonroi     │             │
│   ↓         │             │             │ .Tenancy    │             │
│ Muonroi     │             │             │ .Abstractions│            │
│ .ServiceDisc│             │             │             │ Muonroi     │
│   ↓         │             │             │             │ .RuleEngine │
│ Muonroi     │             │             │             │ .Runtime    │
│ .Kubernetes │             │             │             │   ↓         │
│             │             │             │             │ Muonroi.*   │
│             │             │             │             │ (existing)  │
└─────────────┴─────────────┴─────────────┴─────────────┴─────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────────────┐
│              Abstractions Layer (Contracts & Interfaces)            │
├─────────────────────────────────────────────────────────────────────┤
│  Muonroi.Data.Abstractions                                          │
│  Muonroi.Caching.Abstractions                                       │
│  Muonroi.Messaging.Abstractions                                     │
│  Muonroi.BackgroundJobs.Abstractions                                │
│  Muonroi.Tenancy.Abstractions                                       │
└─────────────────────────────┬───────────────────────────────────────┘
                              │
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                    Core Foundation Layer                            │
├─────────────────────────────────────────────────────────────────────┤
│  Muonroi.Core.Abstractions (Interfaces, Contracts)                  │
│                          ↑                                          │
│  Muonroi.Core (Primitives, Base Classes, Extensions)                │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                    External Dependencies                            │
│  (Microsoft.Extensions.*, EF Core, Grpc, etc.)                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 📦 Detailed Dependency Chains

### Example 1: Simple API with Auth + EF Core + Redis

```
MyApi.csproj
  │
  ├─→ Muonroi.Core
  │     └─→ Microsoft.Extensions.Primitives
  │
  ├─→ Muonroi.Auth
  │     ├─→ Muonroi.Core
  │     ├─→ Microsoft.AspNetCore.Authentication.JwtBearer
  │     └─→ BCrypt.Net-Core
  │
  ├─→ Muonroi.Data.EntityFrameworkCore.PostgreSQL
  │     ├─→ Muonroi.Data.Abstractions
  │     │     └─→ Muonroi.Core
  │     ├─→ Muonroi.Data.EntityFrameworkCore
  │     │     ├─→ Muonroi.Data.Abstractions
  │     │     └─→ Microsoft.EntityFrameworkCore
  │     └─→ Npgsql.EntityFrameworkCore.PostgreSQL
  │
  ├─→ Muonroi.Caching.Redis
  │     ├─→ Muonroi.Caching.Abstractions
  │     │     └─→ Muonroi.Core
  │     └─→ StackExchange.Redis
  │
  └─→ Muonroi.AspNetCore
        ├─→ Muonroi.Core
        └─→ Microsoft.AspNetCore.Mvc

Total packages: 5 focused packages
Total dependencies: ~35 (vs 156 in monolith)
```

---

### Example 2: Event-Driven Microservice with Kafka + Grpc

```
EventService.csproj
  │
  ├─→ Muonroi.Core
  │
  ├─→ Muonroi.Grpc
  │     ├─→ Muonroi.Core
  │     └─→ Grpc.AspNetCore.Server
  │
  ├─→ Muonroi.Messaging.MassTransit.Kafka
  │     ├─→ Muonroi.Messaging.Abstractions
  │     │     └─→ Muonroi.Core
  │     ├─→ Muonroi.Messaging.MassTransit
  │     │     ├─→ Muonroi.Messaging.Abstractions
  │     │     └─→ MassTransit
  │     └─→ MassTransit.Kafka
  │
  ├─→ Muonroi.ServiceDiscovery.Consul
  │     ├─→ Muonroi.Core
  │     └─→ Consul
  │
  └─→ Muonroi.Observability
        ├─→ Muonroi.Core
        ├─→ OpenTelemetry.Extensions.Hosting
        └─→ Serilog.AspNetCore

Total packages: 5 focused packages
Total dependencies: ~40 (vs 156 in monolith)
```

---

### Example 3: Background Worker with Hangfire + Dapper

```
WorkerService.csproj
  │
  ├─→ Muonroi.Core
  │
  ├─→ Muonroi.Data.Dapper
  │     ├─→ Muonroi.Data.Abstractions
  │     │     └─→ Muonroi.Core
  │     ├─→ Dapper
  │     └─→ Dapper.Extensions.NetCore
  │
  ├─→ Muonroi.BackgroundJobs.Hangfire
  │     ├─→ Muonroi.BackgroundJobs.Abstractions
  │     │     └─→ Muonroi.Core
  │     └─→ Hangfire.AspNetCore
  │
  └─→ Muonroi.Caching.Memory
        ├─→ Muonroi.Caching.Abstractions
        │     └─→ Muonroi.Core
        └─→ Microsoft.Extensions.Caching.Memory

Total packages: 4 focused packages
Total dependencies: ~25 (vs 156 in monolith)
```

---

## 🔀 Package Relationships

### Core Layer
```
Muonroi.Core  ←── Everything depends on this
    ↑
Muonroi.Core.Abstractions  ←── Contracts layer
```

### Abstractions Pattern
```
Feature.Abstractions  ←── Contracts
    ↑
Feature.Implementation  ←── Concrete implementation
    ↑
Feature.ExtensionX  ←── Specific provider (optional)
```

**Example: Caching**
```
Muonroi.Caching.Abstractions
    ↑
    ├── Muonroi.Caching.Memory
    └── Muonroi.Caching.Redis
```

**Example: Data**
```
Muonroi.Data.Abstractions
    ↑
    ├── Muonroi.Data.EntityFrameworkCore
    │     ↑
    │     ├── Muonroi.Data.EntityFrameworkCore.SqlServer
    │     ├── Muonroi.Data.EntityFrameworkCore.PostgreSQL
    │     └── Muonroi.Data.EntityFrameworkCore.MySQL
    └── Muonroi.Data.Dapper
```

**Example: Multi-tenant**
```
Muonroi.Tenancy.Abstractions
    ↑
Muonroi.Tenancy.Core
    ↑
Muonroi.Tenancy
```

**Example: Rule Engine runtime alignment**
```
Muonroi.RuleEngine.Abstractions
    ↑
Muonroi.RuleEngine.Core / Muonroi.RuleEngine.NRules / Muonroi.RuleEngine.DecisionTable
    ↑
Muonroi.RuleEngine.Runtime
```

---

## 🎯 Dependency Direction Rules

### ✅ Allowed Dependencies
- Feature → Core (always)
- Feature → Feature.Abstractions (always)
- Implementation → Abstractions (always)
- Extension → Implementation (for specific providers)
- Web → Core features (composition)

### ❌ Forbidden Dependencies
- Core → Feature (circular dependency)
- Abstractions → Implementation (inverted)
- Feature A → Feature B (coupling)
  - Instead: Feature A → Core, Feature B → Core
  - Or: Feature A → Shared.Abstractions ← Feature B

---

## 📐 Layer Separation

```
┌──────────────────────────────────────┐
│     Application Layer (Consumer)     │  ← Your code
├──────────────────────────────────────┤
│     Feature Packages                 │  ← Pick features
├──────────────────────────────────────┤
│     Abstractions Layer               │  ← Contracts
├──────────────────────────────────────┤
│     Core Layer                       │  ← Foundation
├──────────────────────────────────────┤
│     External Dependencies            │  ← Microsoft, OSS
└──────────────────────────────────────┘
```

**Dependency Flow**: Always points downward ↓

---

## 🔍 Circular Dependency Prevention

### Problem Scenario
```
❌ BAD:
Muonroi.Auth → Muonroi.Data  (Auth needs User repository)
Muonroi.Data → Muonroi.Auth  (Data needs User claims)
Result: Circular dependency!
```

### Solution
```
✅ GOOD:
Muonroi.Auth → Muonroi.Core.Abstractions ← Muonroi.Data
Both depend on shared abstractions, not each other
```

---

## 🚀 Migration Path Visualization

### Current (Monolith)
```
┌─────────────────────────────────┐
│   Muonroi.BuildingBlock         │
│   (405 files, 156 deps)         │
│                                 │
│  Contains:                      │
│  • Auth                         │
│  • Data (EF + Dapper)           │
│  • Caching                      │
│  • Messaging                    │
│  • Grpc                         │
│  • SignalR                      │
│  • Observability                │
│  • Background Jobs              │
│  • ... and 20+ more features    │
└─────────────────────────────────┘
         │
         │ Consumer installs this
         ↓
   ❌ Gets EVERYTHING
```

### After Refactoring (Modular)
```
┌────────┬────────┬────────┬────────┬────────┐
│ Core   │ Auth   │ Data   │ Cache  │ Web    │
│ (Base) │ (JWT)  │ (EF)   │ (Redis)│ (MVC)  │
└────────┴────────┴────────┴────────┴────────┘
    │        │        │        │        │
    └────────┴────────┴────────┴────────┘
                    │
         Consumer picks 5 packages
                    ↓
           ✅ Gets only what's needed
```

---

## 📊 Package Size Estimates (After Split)

| Package                                 | Files | Deps | Size   |
|----------------------------------------|-------|------|--------|
| Muonroi.Core                           | ~50   | ~5   | 200 KB |
| Muonroi.Core.Abstractions              | ~30   | ~2   | 100 KB |
| Muonroi.Auth                           | ~40   | ~8   | 300 KB |
| Muonroi.AuthZ                          | ~30   | ~5   | 250 KB |
| Muonroi.Tenancy.Abstractions           | ~8    | ~2   | 80 KB  |
| Muonroi.Tenancy.Core                   | ~12   | ~4   | 120 KB |
| Muonroi.Tenancy                        | ~10   | ~4   | 100 KB |
| Muonroi.Data.EntityFrameworkCore       | ~40   | ~10  | 400 KB |
| Muonroi.Data.Dapper                    | ~25   | ~8   | 250 KB |
| Muonroi.Caching.Redis                  | ~15   | ~6   | 200 KB |
| Muonroi.Messaging.MassTransit.RabbitMQ | ~20   | ~12  | 350 KB |
| Muonroi.Grpc                           | ~20   | ~10  | 300 KB |
| Muonroi.AspNetCore                     | ~50   | ~15  | 500 KB |
| Muonroi.Observability                  | ~25   | ~18  | 450 KB |
| **Total for typical API**              | ~200  | ~35  | **3 MB**|
| **Current Monolith**                   | 405   | 156  | **10+ MB** |
| **Reduction**                          | -50%  | -78% | **-70%** |

---

## ✅ Benefits Visualization

### Download Size
```
Before:  ████████████████████████ 10.5 MB
After:   ███████ 3.2 MB
         └────────────────────┘
         70% reduction
```

### Dependencies
```
Before:  ████████████████████████████ 156 packages
After:   ████████ 35 packages
         └────────────────────────┘
         78% reduction
```

### Build Time
```
Before:  ████████████ 45 seconds
After:   ████ 15 seconds
         └────────┘
         67% faster
```

---

## 🎓 Best Practice Alignment

### ASP.NET Core Pattern
```
Our approach mirrors Microsoft's strategy:

Microsoft.AspNetCore.App (metapackage)
  ├── Microsoft.AspNetCore.Authentication.*
  ├── Microsoft.AspNetCore.Caching.*
  └── Microsoft.AspNetCore.Grpc

Similarly:
Muonroi.BuildingBlock.All (metapackage)
  ├── Muonroi.Auth
  ├── Muonroi.Caching.*
  └── Muonroi.Grpc
```

---

**Next**: See [REFACTORING_PLAN.md](./REFACTORING_PLAN.md) for detailed implementation steps.
