# Muonroi.BuildingBlock Migration Implementation Plan
## From Monolith to 40 Modular Packages (.NET 8)

**Status**: 40 packages created, need content migration
**Target**: .NET 8 (downgrade from .NET 9)
**Goals**:
- Fill all 40 packages with proper content
- Fix naming convention (remove M* prefix abuse)
- Each package passes its own tests
- Create samples for each package

---

## 📊 Current State

### Packages Created (40 total)
```
✅ Created (empty, need content):
   - Muonroi.Core
   - Muonroi.Core.Abstractions
   - Muonroi.Data.Abstractions
   - Muonroi.Data.EntityFrameworkCore
   - Muonroi.Data.Dapper
   - Muonroi.Caching.Abstractions
   - Muonroi.Caching.Memory
   - Muonroi.Caching.Redis
   - Muonroi.Messaging.Abstractions
   - Muonroi.Messaging.MassTransit
   - Muonroi.Mediator
   - Muonroi.Grpc
   - Muonroi.SignalR
   - Muonroi.AspNetCore
   - Muonroi.AspNetCore.OpenApi
   - Muonroi.BackgroundJobs.Abstractions
   - Muonroi.BackgroundJobs.Hangfire
   - Muonroi.BackgroundJobs.Quartz
   - Muonroi.ServiceDiscovery.Consul
   - Muonroi.Kubernetes
   - Muonroi.Resilience
   - Muonroi.Observability
   - Muonroi.Governance
   - Muonroi.Tenancy.Abstractions
   - Muonroi.Tenancy.Core
   - Muonroi.BuildingBlock.All (metapackage)

✅ Already have content (enhance):
   - Muonroi.Auth
   - Muonroi.AuthZ
   - Muonroi.Bff
   - Muonroi.Tenancy
   - Muonroi.RuleEngine.* (8 packages)
   - Muonroi.Rules

❌ To deprecate:
   - Muonroi.BuildingBlock (old monolith)
```

---

## 🎯 Phase 1: .NET 8 Downgrade & Core Foundation (Week 1-2)

### Task 1.1: Downgrade all .csproj to .NET 8
**Files to update**: All 40 *.csproj files

**Find & Replace**:
```xml
<!-- OLD -->
<TargetFramework>net9.0</TargetFramework>

<!-- NEW -->
<TargetFramework>net8.0</TargetFramework>
```

**Script**:
```bash
# Automated downgrade
find src -name "*.csproj" -exec sed -i 's/net9\.0/net8.0/g' {} \;
find tests -name "*.csproj" -exec sed -i 's/net9\.0/net8.0/g' {} \;
find tools -name "*.csproj" -exec sed -i 's/net9\.0/net8.0/g' {} \;
```

**Package version downgrades**:
```xml
<!-- Microsoft.Extensions.* packages -->
<PackageReference Include="Microsoft.Extensions.*" Version="8.0.*" />

<!-- EF Core -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />

<!-- ASP.NET Core -->
<PackageReference Include="Microsoft.AspNetCore.*" Version="8.0.*" />
```

**Verification**:
```bash
dotnet build -c Release
# All projects should build successfully on .NET 8 SDK
```

---

### Task 1.2: Create Muonroi.Core (Foundation Package)

**Source**: Extract from `Muonroi.BuildingBlock/External/SeedWorks/`, `External/Models/`, `External/Helper/`

**Structure**:
```
Muonroi.Core/
├── SeedWorks/
│   ├── Entity.cs                    ← Base entity
│   ├── AuditableEntity.cs           ← Entity with audit fields
│   ├── IAggregateRoot.cs            ← DDD aggregate root marker
│   ├── ValueObject.cs               ← DDD value object base
│   └── Enumeration.cs               ← Smart enum pattern
├── Models/
│   ├── BaseResponse.cs              ← API response wrapper
│   ├── PaginatedList.cs             ← Pagination model
│   └── ErrorDetails.cs              ← Error response model
├── Exceptions/
│   ├── DomainException.cs           ← Business logic exceptions
│   ├── NotFoundException.cs         ← 404 exceptions
│   ├── ValidationException.cs       ← Validation failures
│   └── UnauthorizedException.cs     ← 401 exceptions
├── Extensions/
│   ├── StringExtensions.cs          ← String helpers
│   ├── DateTimeExtensions.cs        ← DateTime helpers
│   ├── EnumerableExtensions.cs      ← LINQ helpers
│   └── ObjectExtensions.cs          ← Object mapping helpers
└── Utilities/
    ├── Guard.cs                     ← Argument validation
    ├── Clock.cs                     ← Testable time abstraction
    └── GuidGenerator.cs             ← ID generation

Total: ~30 files, 2000 LOC
```

**Migration mapping**:
```
BuildingBlock/External/SeedWorks/Entity.cs
  → Core/SeedWorks/Entity.cs

BuildingBlock/External/Models/BaseResponse.cs
  → Core/Models/BaseResponse.cs

BuildingBlock/External/Helper/StringHelper.cs
  → Core/Extensions/StringExtensions.cs
```

**.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>Muonroi.Core</PackageId>
    <Version>1.0.0</Version>
    <Description>Core primitives and base classes for Muonroi ecosystem</Description>
  </PropertyGroup>

  <ItemGroup>
    <!-- Minimal dependencies -->
    <PackageReference Include="System.Text.Json" Version="8.0.*" />
  </ItemGroup>
</Project>
```

**Test project**: `tests/Muonroi.Core.Tests/`
```
Muonroi.Core.Tests/
├── SeedWorks/
│   ├── EntityTests.cs
│   └── ValueObjectTests.cs
├── Extensions/
│   ├── StringExtensionsTests.cs
│   └── DateTimeExtensionsTests.cs
└── Utilities/
    └── GuardTests.cs
```

**Sample**: `samples/Muonroi.Core.Sample/`
```csharp
// samples/Muonroi.Core.Sample/Program.cs
using Muonroi.Core.SeedWorks;
using Muonroi.Core.Extensions;

// Example 1: Using base entity
public class Product : Entity
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// Example 2: Using string extensions
var email = "  USER@EXAMPLE.COM  ";
var normalized = email.ToLowerTrimmed(); // "user@example.com"

// Example 3: Using Guard
Guard.Against.NullOrEmpty(productName, nameof(productName));
```

---

### Task 1.3: Create Muonroi.Core.Abstractions (Contracts Package)

**Source**: Extract from `Muonroi.BuildingBlock/Contract/Interfaces/`

**Structure**:
```
Muonroi.Core.Abstractions/
├── Repositories/
│   ├── IRepository.cs               ← Generic repository
│   ├── IReadRepository.cs           ← Read-only repository
│   └── IUnitOfWork.cs               ← UoW pattern
├── Specifications/
│   ├── ISpecification.cs            ← Query specification
│   └── Specification.cs             ← Base specification
├── Services/
│   ├── ICurrentUser.cs              ← Current user accessor
│   ├── IDateTime.cs                 ← Time abstraction
│   └── IEventPublisher.cs           ← Event publishing
└── Queries/
    ├── IQuery.cs                    ← CQRS query marker
    └── IQueryHandler.cs             ← Query handler

Total: ~15 files, 500 LOC
```

**.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Muonroi.Core\Muonroi.Core.csproj" />
  </ItemGroup>
</Project>
```

---

## 🎯 Phase 2: Naming Convention Cleanup (Week 2)

### Problem: Uncontrolled M* Prefix Usage

**Current issues**:
```csharp
// ❌ BAD: Uncontrolled / non-domain-qualified M prefix
public class MAuthInfoContext { }
public class MGenericController<T> { }
public interface IMGenericRepository<T> { }
public class MUserService { }
```

### Naming Convention Rules (Brand-Preserved)

**✅ GOOD: When to use M prefix**
```csharp
// Muonroi-specific attributes
[MExtractAsRule("VAL-001")]       // ✅ OK: Our custom attribute
[MValidation(Required = true)]    // ✅ OK: Our custom attribute

// Muonroi-specific markers
public interface IMuonroiEntity { }  // ✅ OK: Clear Muonroi marker
```

**✅ GOOD: Keep M* but with explicit domain taxonomy**
```csharp
// Domain-qualified M prefix
public class MAuthContext { }                   // was: MAuthInfoContext
public class MWebGenericController<T> { }       // was: MGenericController
public interface IMDataRepository<T> { }        // was: IMGenericRepository
public class MAuthUserService { }               // was: MUserService

// Keep neutral names for framework-required conventions only
public class Program { }                        // ✅ host entry point
```

### Rename Script
```bash
# Find all M-prefixed classes
grep -r "class M[A-Z]" src/ --include="*.cs"

# Generate rename commands
# Manual review required - not all M* should be renamed
```

### Rename mapping (examples):
```
MAuthInfoContext          → MAuthContext
MGenericController        → MWebGenericController
MUserService              → MAuthUserService
MPermissionService        → MAuthZPermissionService
MEFUnitOfWork             → MDataEfUnitOfWork
```

**Keep M prefix for**:
```
[MExtractAsRule]          ← Custom attribute
[MValidation]             ← Custom attribute
MCore/MAuth/MData/...     ← Domain-qualified brand naming
```

---

## 🎯 Phase 3: Data Layer Migration (Week 3-4)

### Task 3.1: Muonroi.Data.Abstractions

**Source**: `BuildingBlock/External/Repositories/`, `External/UnitOfWork/`

**Structure**:
```
Muonroi.Data.Abstractions/
├── Repositories/
│   ├── IRepository.cs
│   ├── IReadRepository.cs
│   └── IAsyncRepository.cs
├── UnitOfWork/
│   ├── IUnitOfWork.cs
│   └── IUnitOfWorkFactory.cs
├── Specifications/
│   ├── ISpecification.cs
│   ├── Specification.cs
│   └── CompositeSpecification.cs
└── Queries/
    ├── IPagedQuery.cs
    ├── PagedResult.cs
    └── QueryOptions.cs
```

**Migration**:
```
BuildingBlock/External/Repositories/IRepository.cs
  → Data.Abstractions/Repositories/IRepository.cs

BuildingBlock/External/UnitOfWork/IUnitOfWork.cs
  → Data.Abstractions/UnitOfWork/IUnitOfWork.cs
```

**Dependencies**:
```xml
<ItemGroup>
  <ProjectReference Include="..\Muonroi.Core.Abstractions\Muonroi.Core.Abstractions.csproj" />
</ItemGroup>
```

**Sample**:
```csharp
// samples/Muonroi.Data.Sample/
public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku);
}

public class ProductSpecification : Specification<Product>
{
    public ProductSpecification(decimal minPrice)
    {
        Criteria = p => p.Price >= minPrice;
    }
}
```

---

### Task 3.2: Muonroi.Data.EntityFrameworkCore

**Source**: `BuildingBlock/External/ORMs/EFCore/`

**Structure**:
```
Muonroi.Data.EntityFrameworkCore/
├── Repositories/
│   ├── EFRepository.cs              ← Generic EF implementation
│   └── EFReadRepository.cs          ← Read-only queries
├── UnitOfWork/
│   ├── EFUnitOfWork.cs              ← EF UoW implementation
│   └── UnitOfWorkFactory.cs         ← Factory pattern
├── Extensions/
│   ├── ModelBuilderExtensions.cs    ← EF configuration helpers
│   ├── QueryableExtensions.cs       ← LINQ extensions
│   └── DbContextExtensions.cs       ← DbContext helpers
├── Interceptors/
│   ├── AuditInterceptor.cs          ← Auto-set audit fields
│   └── SoftDeleteInterceptor.cs     ← Soft delete pattern
└── Configuration/
    └── EntityConfiguration.cs       ← Base configuration
```

**Dependencies**:
```xml
<ItemGroup>
  <ProjectReference Include="..\Muonroi.Data.Abstractions\Muonroi.Data.Abstractions.csproj" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.*" />
</ItemGroup>
```

**Sample**:
```csharp
// samples/Muonroi.Data.EFCore.Sample/
public class AppDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .AddInterceptors(new AuditInterceptor())
            .AddInterceptors(new SoftDeleteInterceptor());
    }
}

// Usage
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

services.AddScoped(typeof(IRepository<>), typeof(EFRepository<>));
services.AddScoped<IUnitOfWork, EFUnitOfWork<AppDbContext>>();
```

---

### Task 3.3: Muonroi.Data.Dapper

**Source**: `BuildingBlock/External/ORMs/Dapper/`

**Structure**:
```
Muonroi.Data.Dapper/
├── Repositories/
│   ├── DapperRepository.cs          ← Dapper implementation
│   └── DapperReadRepository.cs      ← Read-only Dapper
├── UnitOfWork/
│   └── DapperUnitOfWork.cs          ← Transaction management
├── Extensions/
│   ├── DapperExtensions.cs          ← Helper methods
│   └── SqlBuilderExtensions.cs      ← SQL generation
└── TypeHandlers/
    ├── JsonTypeHandler.cs           ← JSON column support
    └── DateTimeOffsetHandler.cs     ← DateTime handling
```

**Sample**:
```csharp
// samples/Muonroi.Data.Dapper.Sample/
public class ProductRepository : DapperRepository<Product>, IProductRepository
{
    public ProductRepository(IDbConnection connection) : base(connection) { }

    public async Task<Product?> GetBySkuAsync(string sku)
    {
        const string sql = "SELECT * FROM Products WHERE Sku = @Sku";
        return await Connection.QueryFirstOrDefaultAsync<Product>(sql, new { Sku = sku });
    }
}
```

---

## 🎯 Phase 4: Caching Layer Migration (Week 4)

### Task 4.1: Muonroi.Caching.Abstractions

**Structure**:
```
Muonroi.Caching.Abstractions/
├── ICacheService.cs
├── ICacheKeyGenerator.cs
├── CacheOptions.cs
├── CacheEntryOptions.cs
└── Strategies/
    ├── IInvalidationStrategy.cs
    └── InvalidationStrategyBase.cs
```

**Sample**:
```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
```

### Task 4.2: Muonroi.Caching.Redis

**Source**: `BuildingBlock/External/Caching/RedisCacheService.cs`

**Structure**:
```
Muonroi.Caching.Redis/
├── RedisCacheService.cs
├── RedisOptions.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
└── Invalidation/
    ├── RedisPubSubInvalidation.cs
    └── RedisInvalidationChannel.cs
```

**Sample**:
```csharp
// Startup
services.AddRedisCache(options =>
{
    options.ConnectionString = "localhost:6379";
    options.InstanceName = "myapp:";
    options.EnablePubSubInvalidation = true;
});

// Usage
var product = await _cache.GetOrSetAsync(
    $"product:{id}",
    () => _repository.GetByIdAsync(id),
    TimeSpan.FromMinutes(10));
```

---

## 🎯 Phase 5: Messaging Layer Migration (Week 5)

### Task 5.1: Muonroi.Messaging.Abstractions

**Structure**:
```
Muonroi.Messaging.Abstractions/
├── IMessageBus.cs
├── IEventPublisher.cs
├── IEventHandler.cs
├── Events/
│   ├── IEvent.cs
│   ├── IIntegrationEvent.cs
│   └── IDomainEvent.cs
└── Messages/
    ├── IMessage.cs
    └── MessageMetadata.cs
```

### Task 5.2: Muonroi.Messaging.MassTransit

**Source**: `BuildingBlock/External/Messaging/`

**Sample**:
```csharp
// Startup
services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost");
        cfg.ConfigureEndpoints(context);
    });
});

// Event
public record OrderCreatedEvent(Guid OrderId, decimal Total) : IIntegrationEvent;

// Consumer
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        // Handle event
    }
}
```

---

## 🎯 Phase 6: Web Layer Migration (Week 6)

### Task 6.1: Muonroi.AspNetCore

**Source**: `BuildingBlock/External/Controller/`, `External/Middleware/`, `External/Filters/`

**Structure**:
```
Muonroi.AspNetCore/
├── Controllers/
│   ├── GenericController.cs         ← Base API controller
│   └── ControllerExtensions.cs      ← Helper methods
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   └── TenantResolutionMiddleware.cs
├── Filters/
│   ├── ValidationFilter.cs
│   ├── AuthorizationFilter.cs
│   └── AuditFilter.cs
├── ModelBinding/
│   └── CustomModelBinders.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

**Sample**:
```csharp
// Program.cs
builder.Services.AddMuonroiAspNetCore(options =>
{
    options.UseExceptionHandling = true;
    options.UseRequestLogging = true;
    options.UseValidationFilter = true;
});

// Controller
public class ProductsController : GenericController<Product, ProductDto>
{
    public ProductsController(IRepository<Product> repository, IMapper mapper)
        : base(repository, mapper)
    {
    }
}
```

---

## 🎯 Phase 7: Test Migration (Week 7-8)

### Test Project Template

**For each package**, create corresponding test project:

```
Package: Muonroi.{Feature}
Test:    Muonroi.{Feature}.Tests

Example:
  src/Muonroi.Core/
  tests/Muonroi.Core.Tests/
```

### Test Project Structure
```
Muonroi.{Feature}.Tests/
├── {Feature}Tests.cs            ← Main functionality tests
├── Extensions/
│   └── ExtensionTests.cs
├── Integration/
│   └── IntegrationTests.cs      ← Integration tests
├── Fixtures/
│   └── TestFixture.cs           ← Test setup/teardown
└── TestData/
    └── TestDataBuilder.cs       ← Test data generation
```

### Test .csproj Template
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.*" />
    <PackageReference Include="xunit" Version="2.6.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.*" />
    <PackageReference Include="FluentAssertions" Version="6.12.*" />
    <PackageReference Include="NSubstitute" Version="5.1.*" />
    <PackageReference Include="Bogus" Version="35.0.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Muonroi.{Feature}\Muonroi.{Feature}.csproj" />
  </ItemGroup>
</Project>
```

### Migrate existing tests

**From**:
```
tests/Muonroi.BuildingBlock.Test/  (monolithic test project)
```

**To**:
```
tests/Muonroi.Core.Tests/
tests/Muonroi.Data.EntityFrameworkCore.Tests/
tests/Muonroi.Caching.Redis.Tests/
... (40 test projects total)
```

**Migration mapping**:
```
BuildingBlock.Test/SeedWorks/EntityTests.cs
  → Core.Tests/SeedWorks/EntityTests.cs

BuildingBlock.Test/Repositories/RepositoryTests.cs
  → Data.EntityFrameworkCore.Tests/Repositories/EFRepositoryTests.cs

BuildingBlock.Test/Caching/CacheTests.cs
  → Caching.Redis.Tests/RedisCacheServiceTests.cs
```

---

## 🎯 Phase 8: Sample Projects (Week 8-9)

### Sample Structure

```
samples/
├── Muonroi.Core.Sample/
├── Muonroi.Data.EFCore.Sample/
├── Muonroi.Caching.Redis.Sample/
├── Muonroi.Messaging.MassTransit.Sample/
├── Muonroi.Auth.Sample/
├── Muonroi.AspNetCore.Complete.Sample/    ← Full-stack example
└── README.md
```

### Sample Template

**Minimal Console Sample**:
```csharp
// samples/Muonroi.{Feature}.Sample/Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.{Feature};

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.Add{Feature}Services();

var app = builder.Build();

// Example usage
var service = app.Services.GetRequiredService<I{Feature}Service>();
await service.DoSomethingAsync();

Console.WriteLine("{Feature} sample completed!");
```

**Web API Sample** (for AspNetCore, Auth, etc.):
```csharp
// samples/Muonroi.AspNetCore.Complete.Sample/
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMuonroiCore();
builder.Services.AddMuonroiAuth(builder.Configuration);
builder.Services.AddMuonroiTenancy();
builder.Services.AddMuonroiCaching();
builder.Services.AddMuonroiAspNetCore();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Sample for each package category:

**1. Core Sample**:
```csharp
// Demonstrates: Entity, ValueObject, Guard, Extensions
var product = new Product
{
    Id = Guid.NewGuid(),
    Name = "Sample Product",
    Price = 99.99m
};

Guard.Against.Null(product.Name, nameof(product.Name));
var slug = product.Name.ToSlug(); // "sample-product"
```

**2. Data Sample**:
```csharp
// Demonstrates: Repository, UnitOfWork, Specification
var spec = new ProductSpecification(minPrice: 50m);
var products = await _repository.FindAsync(spec);

using var uow = _uowFactory.Create();
await _repository.AddAsync(newProduct);
await uow.SaveChangesAsync();
```

**3. Caching Sample**:
```csharp
// Demonstrates: Redis cache with pub/sub invalidation
var product = await _cache.GetOrSetAsync(
    $"product:{id}",
    () => _repository.GetByIdAsync(id),
    TimeSpan.FromMinutes(10));

await _cache.RemoveByPatternAsync("product:*");
```

**4. Messaging Sample**:
```csharp
// Demonstrates: MassTransit event publishing
await _eventPublisher.PublishAsync(new OrderCreatedEvent(orderId, total));

// Consumer
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        Console.WriteLine($"Order {context.Message.OrderId} created!");
    }
}
```

**5. Auth Sample**:
```csharp
// Demonstrates: JWT token generation, validation
var token = await _authService.GenerateTokenAsync(user);

[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    // Only admins can access
}
```

**6. Complete Sample** (combines multiple packages):
```csharp
// Full e-commerce API demonstrating:
// - Auth (JWT)
// - Multi-tenancy
// - EF Core repositories
// - Redis caching
// - MassTransit messaging
// - AspNetCore controllers
```

---

## 🎯 Phase 9: Metapackage (Week 9)

### Muonroi.BuildingBlock.All

**Purpose**: Backward compatibility, quick prototyping

**.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <Description>Convenience metapackage containing all Muonroi packages. For production, prefer individual packages.</Description>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core -->
    <ProjectReference Include="..\Muonroi.Core\Muonroi.Core.csproj" />
    <ProjectReference Include="..\Muonroi.Core.Abstractions\Muonroi.Core.Abstractions.csproj" />

    <!-- Auth -->
    <ProjectReference Include="..\Muonroi.Auth\Muonroi.Auth.csproj" />
    <ProjectReference Include="..\Muonroi.AuthZ\Muonroi.AuthZ.csproj" />

    <!-- Data -->
    <ProjectReference Include="..\Muonroi.Data.Abstractions\Muonroi.Data.Abstractions.csproj" />
    <ProjectReference Include="..\Muonroi.Data.EntityFrameworkCore\Muonroi.Data.EntityFrameworkCore.csproj" />
    <ProjectReference Include="..\Muonroi.Data.Dapper\Muonroi.Data.Dapper.csproj" />

    <!-- Caching -->
    <ProjectReference Include="..\Muonroi.Caching.Redis\Muonroi.Caching.Redis.csproj" />
    <ProjectReference Include="..\Muonroi.Caching.Memory\Muonroi.Caching.Memory.csproj" />

    <!-- Messaging -->
    <ProjectReference Include="..\Muonroi.Messaging.MassTransit\Muonroi.Messaging.MassTransit.csproj" />
    <ProjectReference Include="..\Muonroi.Mediator\Muonroi.Mediator.csproj" />

    <!-- Communication -->
    <ProjectReference Include="..\Muonroi.Grpc\Muonroi.Grpc.csproj" />
    <ProjectReference Include="..\Muonroi.SignalR\Muonroi.SignalR.csproj" />
    <ProjectReference Include="..\Muonroi.Bff\Muonroi.Bff.csproj" />

    <!-- Web -->
    <ProjectReference Include="..\Muonroi.AspNetCore\Muonroi.AspNetCore.csproj" />
    <ProjectReference Include="..\Muonroi.AspNetCore.OpenApi\Muonroi.AspNetCore.OpenApi.csproj" />

    <!-- Infrastructure -->
    <ProjectReference Include="..\Muonroi.Observability\Muonroi.Observability.csproj" />
    <ProjectReference Include="..\Muonroi.BackgroundJobs.Hangfire\Muonroi.BackgroundJobs.Hangfire.csproj" />
    <ProjectReference Include="..\Muonroi.ServiceDiscovery.Consul\Muonroi.ServiceDiscovery.Consul.csproj" />
    <ProjectReference Include="..\Muonroi.Resilience\Muonroi.Resilience.csproj" />

    <!-- Multi-tenancy -->
    <ProjectReference Include="..\Muonroi.Tenancy\Muonroi.Tenancy.csproj" />

    <!-- Rule Engine -->
    <ProjectReference Include="..\Muonroi.RuleEngine.Core\Muonroi.RuleEngine.Core.csproj" />
    <ProjectReference Include="..\Muonroi.RuleEngine.DecisionTable\Muonroi.RuleEngine.DecisionTable.csproj" />

    <!-- ... all 40 packages -->
  </ItemGroup>
</Project>
```

**README.md**:
```markdown
# Muonroi.BuildingBlock.All

⚠️ **Warning**: This is a convenience metapackage that includes ALL Muonroi packages.

For production applications, we recommend installing only the packages you need:

```bash
# Instead of:
dotnet add package Muonroi.BuildingBlock.All

# Use specific packages:
dotnet add package Muonroi.Core
dotnet add package Muonroi.Auth
dotnet add package Muonroi.Data.EntityFrameworkCore
```

See [Package Selection Guide](../docs/package-selection.md) for recommendations.
```

---

## 📋 Complete Package Checklist

For each of the 40 packages, complete:

### ✅ Package Implementation Checklist

- [ ] **Project Setup**
  - [ ] Create .csproj with .NET 8
  - [ ] Set proper PackageId, Version, Description
  - [ ] Configure dependencies

- [ ] **Code Migration**
  - [ ] Identify source files from Muonroi.BuildingBlock
  - [ ] Move files to new package
  - [ ] Fix namespaces
  - [ ] Remove M* prefix abuse
  - [ ] Update using statements
  - [ ] Fix internal/public visibility

- [ ] **Test Project**
  - [ ] Create test project (.NET 8)
  - [ ] Migrate relevant tests from BuildingBlock.Test
  - [ ] Add new tests for new features
  - [ ] Ensure 100% of old tests pass
  - [ ] Add integration tests if applicable

- [ ] **Sample Project**
  - [ ] Create sample console/web app
  - [ ] Demonstrate main features
  - [ ] Include README with usage examples
  - [ ] Ensure sample builds and runs

- [ ] **Documentation**
  - [ ] Add XML documentation comments
  - [ ] Create package README.md
  - [ ] Add to main docs site
  - [ ] Add migration guide if breaking changes

- [ ] **Verification**
  - [ ] `dotnet build -c Release` succeeds
  - [ ] `dotnet test` passes all tests
  - [ ] `dotnet pack` creates NuGet package
  - [ ] No warnings (treat warnings as errors)

---

## 🚀 Execution Order

Execute packages in dependency order (bottom-up):

### Week 1-2: Foundation
1. Muonroi.Core
2. Muonroi.Core.Abstractions
3. Downgrade all .csproj to .NET 8

### Week 3: Data Layer
4. Muonroi.Data.Abstractions
5. Muonroi.Data.EntityFrameworkCore
6. Muonroi.Data.Dapper

### Week 4: Caching & Messaging Abstractions
7. Muonroi.Caching.Abstractions
8. Muonroi.Caching.Memory
9. Muonroi.Caching.Redis
10. Muonroi.Messaging.Abstractions

### Week 5: Messaging & Mediator
11. Muonroi.Messaging.MassTransit
12. Muonroi.Mediator

### Week 6: Web & Communication
13. Muonroi.AspNetCore
14. Muonroi.AspNetCore.OpenApi
15. Muonroi.Grpc
16. Muonroi.SignalR

### Week 7: Infrastructure
17. Muonroi.Observability
18. Muonroi.BackgroundJobs.Abstractions
19. Muonroi.BackgroundJobs.Hangfire
20. Muonroi.BackgroundJobs.Quartz
21. Muonroi.ServiceDiscovery.Consul
22. Muonroi.Kubernetes
23. Muonroi.Resilience

### Week 8: Enhancement Packages
24. Muonroi.Governance
25. Muonroi.Tenancy.Abstractions
26. Muonroi.Tenancy.Core
27. Enhance existing: Auth, AuthZ, Tenancy, Bff

### Week 9: Finalization
28. Muonroi.BuildingBlock.All (metapackage)
29. Create all 40 sample projects
30. Migration guide

---

## 📊 Success Metrics

Track completion:
```
Progress: [====>    ] 15/40 packages (37.5%)

✅ Core Foundation:    2/2  (100%)
✅ Data Layer:         3/3  (100%)
🔄 Caching:            2/3  (67%)
⏳ Messaging:          0/3  (0%)
⏳ Web:                0/4  (0%)
⏳ Infrastructure:     0/7  (0%)
⏳ Others:             0/18 (0%)
```

---

**Next**: Start with Phase 1 - Create script để automate .NET 8 downgrade và begin Muonroi.Core migration?
