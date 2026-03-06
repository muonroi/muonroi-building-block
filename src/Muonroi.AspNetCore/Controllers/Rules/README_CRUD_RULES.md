# Auto CRUD with Business Rules

## Overview

The Auto CRUD system now supports **Business Rule Engine integration**, allowing you to apply complex business logic to CRUD operations without writing manual controller code.

## Problem Solved

Previously, Auto CRUD had a critical limitation (điểm chí mạng): **it couldn't apply business rules**. Now you can hook business logic at key points:

- **BeforeCreate** - Validate data before creating entities
- **AfterCreate** - Execute side effects after creation (e.g., send notifications)
- **BeforeUpdate** - Validate updates, check permissions
- **AfterUpdate** - Trigger workflows after updates
- **BeforeDelete** - Prevent deletion based on business rules
- **AfterDelete** - Clean up related data after deletion

## Quick Start

### 1. Create Your Entity

```csharp
public class Product : MEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
}
```

### 2. Create Business Rules

```csharp
public class ProductPriceValidationRule : IRule<CrudContext<Product>>
{
    public string Code => "PRODUCT_PRICE_VALIDATION";
    public string Name => "Product Price Validation";
    public int Order => 10;
    public HookPoint HookPoint => HookPoint.BeforeCreate;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();
    public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

    public async Task<RuleResult> EvaluateAsync(
        CrudContext<Product> context,
        FactBag facts,
        CancellationToken ct)
    {
        if (context.Entity.Price <= 0)
        {
            context.ValidationErrors.Add("Price must be greater than 0");
            return RuleResult.Failure("Invalid price");
        }

        if (context.Entity.Stock < 0)
        {
            context.ValidationErrors.Add("Stock cannot be negative");
            return RuleResult.Failure("Invalid stock");
        }

        return RuleResult.Passed();
    }

    public Task ExecuteAsync(CrudContext<Product> context, CancellationToken ct)
    {
        // Validation only, no execution needed
        return Task.CompletedTask;
    }
}
```

### 3. Register Rules in Program.cs

```csharp
// Register CRUD rules for Product entity
builder.Services.AddCrudRules<Product>(services =>
{
    // Add validation rule
    services.AddCrudRule<Product, ProductPriceValidationRule>();

    // Add more rules as needed
    services.AddCrudRule<Product, ProductStockManagementRule>();
    services.AddCrudRule<Product, ProductNotificationRule>();
});
```

### 4. Create Auto CRUD Controller (No Manual Code!)

```csharp
[AllowAnonymous]
[GenericCrudPermission(SkipPermissionCheck = true)]
public class ProductController(YourDbContext dbContext)
    : MGenericController<Product, YourDbContext>(dbContext)
{
    // That's it! All CRUD + Business Rules work automatically
}
```

## Advanced Examples

### Example 1: Stock Management Rule (AfterCreate)

```csharp
public class ProductStockManagementRule : IRule<CrudContext<Product>>
{
    private readonly IInventoryService _inventoryService;

    public ProductStockManagementRule(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public string Code => "PRODUCT_STOCK_MANAGEMENT";
    public HookPoint HookPoint => HookPoint.AfterCreate;
    public int Order => 20;

    public async Task<RuleResult> EvaluateAsync(
        CrudContext<Product> context,
        FactBag facts,
        CancellationToken ct)
    {
        // Always pass - this is a side effect rule
        return RuleResult.Passed();
    }

    public async Task ExecuteAsync(CrudContext<Product> context, CancellationToken ct)
    {
        // Reserve stock in inventory system
        await _inventoryService.ReserveStockAsync(
            context.Entity.EntityId,
            context.Entity.Stock,
            ct);
    }
}
```

### Example 2: Price Change Audit (BeforeUpdate)

```csharp
public class ProductPriceAuditRule : IRule<CrudContext<Product>>
{
    private readonly IAuditService _auditService;

    public ProductPriceAuditRule(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public string Code => "PRODUCT_PRICE_AUDIT";
    public HookPoint HookPoint => HookPoint.BeforeUpdate;
    public int Order => 5;

    public async Task<RuleResult> EvaluateAsync(
        CrudContext<Product> context,
        FactBag facts,
        CancellationToken ct)
    {
        var oldPrice = context.OriginalEntity?.Price ?? 0;
        var newPrice = context.Entity.Price;

        if (oldPrice != newPrice)
        {
            // Log price change
            await _auditService.LogPriceChangeAsync(
                context.Entity.EntityId,
                oldPrice,
                newPrice,
                context.UserId,
                ct);
        }

        return RuleResult.Passed();
    }

    public Task ExecuteAsync(CrudContext<Product> context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
```

### Example 3: Prevent Deletion if Orders Exist (BeforeDelete)

```csharp
public class ProductDeletionGuardRule : IRule<CrudContext<Product>>
{
    private readonly IOrderService _orderService;

    public ProductDeletionGuardRule(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public string Code => "PRODUCT_DELETION_GUARD";
    public HookPoint HookPoint => HookPoint.BeforeDelete;
    public int Order => 1;

    public async Task<RuleResult> EvaluateAsync(
        CrudContext<Product> context,
        FactBag facts,
        CancellationToken ct)
    {
        var hasOrders = await _orderService.HasOrdersForProductAsync(
            context.Entity.EntityId,
            ct);

        if (hasOrders)
        {
            context.CancelOperation = true;
            context.CancellationReason = "Cannot delete product with existing orders";
            return RuleResult.Failure("Product has orders");
        }

        return RuleResult.Passed();
    }

    public Task ExecuteAsync(CrudContext<Product> context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
```

## Rule Execution Flow

### Create Operation
1. Validate ModelState
2. Check permissions
3. Set audit fields (CreationTime, CreatorUserId)
4. **Execute BeforeCreate rules** ← Your business logic here
5. If rules pass, save to database
6. **Execute AfterCreate rules** ← Side effects (notifications, etc.)
7. Return response

### Update Operation
1. Validate ModelState
2. Check permissions
3. Load existing entity
4. **Execute BeforeUpdate rules** ← Validation, audit
5. If rules pass, apply changes
6. Update audit fields
7. Save to database
8. **Execute AfterUpdate rules** ← Trigger workflows
9. Return response

### Delete Operation
1. Check permissions
2. Load existing entity
3. **Execute BeforeDelete rules** ← Business validations
4. If rules pass, soft delete (set IsDeleted = true)
5. Set deletion audit fields
6. Save to database
7. **Execute AfterDelete rules** ← Cleanup
8. Return response

## CrudContext API

The `CrudContext<TEntity>` object passed to rules contains:

```csharp
public class CrudContext<TEntity>
{
    // The entity being operated on
    TEntity Entity { get; set; }

    // Original state (for Update operations)
    TEntity? OriginalEntity { get; set; }

    // Create, Update, Delete, or Read
    CrudOperationType OperationType { get; set; }

    // Current user ID
    Guid? UserId { get; set; }

    // Current tenant ID (multi-tenant scenarios)
    string? TenantId { get; set; }

    // Add validation errors here
    List<string> ValidationErrors { get; }

    // Custom metadata
    Dictionary<string, object?> Metadata { get; }

    // Set to true to cancel the operation
    bool CancelOperation { get; set; }

    // Reason for cancellation
    string? CancellationReason { get; set; }
}
```

## Best Practices

✅ **DO:**
- Keep rules focused on single responsibility
- Use `Order` property to control execution sequence
- Use `DependsOn` for rule dependencies
- Validate in BeforeCreate/BeforeUpdate rules
- Execute side effects in AfterCreate/AfterUpdate rules
- Set `CancelOperation = true` to prevent operation
- Add clear error messages to `ValidationErrors`

❌ **DON'T:**
- Don't modify the database directly in rules (let the controller handle it)
- Don't throw exceptions for validation failures (return RuleResult.Failure)
- Don't create circular dependencies between rules
- Don't put business logic in controllers when rules can handle it

## Testing

```csharp
[Fact]
public async Task Create_WithInvalidPrice_ShouldReturnBadRequest()
{
    // Arrange
    var product = new Product
    {
        Name = "Test Product",
        Price = -10, // Invalid!
        Stock = 100
    };

    // Act
    var response = await _controller.Create(product, CancellationToken.None);

    // Assert
    var badRequest = Assert.IsType<BadRequestObjectResult>(response);
    var responseObj = Assert.IsType<MResponse<object>>(badRequest.Value);
    Assert.Contains("Price must be greater than 0", responseObj.ErrorMessage);
}
```

## Performance

- Rules execute in dependency order (topological sort)
- Failed rules short-circuit execution
- AfterCreate/AfterUpdate/AfterDelete rules run asynchronously
- Use `Order` to optimize critical path execution

## Summary

Auto CRUD + Rule Engine = **Powerful business logic without manual controller code**

🎯 Just create entities, define rules, and get full CRUD APIs with business logic automatically!
