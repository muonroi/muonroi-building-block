namespace Muonroi.AspNetCore.Controllers;

/// <inheritdoc />
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class MGenericController<TEntity, TDbContext>(
    TDbContext dbContext,
    ILicenseGuard licenseGuard,
    MTokenInfo tokenInfo,
    IConfiguration configuration,
    IMDateTimeService dateTimeService) : ControllerBase
    where TEntity : MEntity
    where TDbContext : MDbContext
{
    private static readonly bool _isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(typeof(TEntity));
    private static readonly PropertyInfo? _tenantIdProperty = typeof(TEntity).GetProperty("TenantId");
    private readonly bool _multiTenantEnabled =
        tokenInfo.MultiTenantEnabled || configuration.GetValue("MultiTenantConfigs:Enabled", false);
    private readonly bool _requireTenantClaimForAuthenticatedUser =
        configuration.GetValue("MultiTenantConfigs:RequireTenantClaimForAuthenticatedUser", true);

    private RuleOrchestrator<CrudContext<TEntity>>? _ruleOrchestrator;

/// <inheritdoc />
    [HttpGet]
    public virtual async Task<IActionResult> Get([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        licenseGuard.EnsureValid("api.list", typeof(TEntity).Name);
        // Check permission
        if (!await CheckPermissionAsync("View", cancellationToken))
        {
            return Forbid();
        }

        IQueryable<TEntity> query = ApplyTenantFilter(dbContext.Set<TEntity>().AsNoTracking().Where(x => !x.IsDeleted));
        List<TEntity> items = await query
            .OrderByDescending(x => x.CreationTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        int total = await query.CountAsync(cancellationToken);

        MResponse<object> value = new()
        {
            Result = new
            {
                Items = items,
                CurrentPage = pageIndex,
                PageSize = pageSize,
                RowCount = total
            }
        };
        return Ok(value);
    }

/// <inheritdoc />
    [HttpGet("{id:guid}")]
    public virtual async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        licenseGuard.EnsureValid("api.get", typeof(TEntity).Name);
        // Check permission
        if (!await CheckPermissionAsync("View", cancellationToken))
        {
            return Forbid();
        }

        IQueryable<TEntity> query = ApplyTenantFilter(dbContext.Set<TEntity>().AsNoTracking());
        TEntity? item = await query.FirstOrDefaultAsync(x => x.EntityId == id && !x.IsDeleted, cancellationToken);

        if (item == null)
        {
            MResponse<object> response = new();
            response.SetError("EntityNotFound");
            return NotFound(response);
        }

        MResponse<TEntity> value = new()
        {
            Result = item
        };
        return Ok(value);
    }

/// <inheritdoc />
    [HttpPost]
    public virtual async Task<IActionResult> Create([FromBody] TEntity entity, CancellationToken cancellationToken)
    {
        licenseGuard.EnsureValid("api.create", typeof(TEntity).Name);
        // Validate input
        if (!ModelState.IsValid)
        {
            MResponse<object> response = new();
            response.SetError("ValidationFailed");
            return BadRequest(response);
        }

        // Check permission
        if (!await CheckPermissionAsync("Create", cancellationToken))
        {
            return Forbid();
        }

        // Get current user
        Guid? currentUserId = GetCurrentUserId();

        // Set audit fields
        entity.EntityId = Guid.NewGuid();
        entity.CreationTime = dateTimeService.UtcNow();
        entity.LastModificationTime = dateTimeService.UtcNow();
        entity.IsDeleted = false;
        entity.CreatorUserId = currentUserId ?? Guid.Empty;
        entity.LastModificationUserId = currentUserId;

        // SECURITY: Set TenantId for multi-tenant entities
        SetTenantId(entity);

        // Execute BeforeCreate business rules
        CrudContext<TEntity> beforeContext = CreateCrudContext(entity, CrudOperationType.Create);
        (bool beforeSuccess, string? beforeError) = await ExecuteRulesAsync(beforeContext, HookPoint.BeforeRule, cancellationToken);
        if (!beforeSuccess)
        {
            MResponse<object> response = new();
            response.SetError(beforeError ?? "Business rule validation failed");
            return BadRequest(response);
        }

        await dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Execute AfterCreate business rules
        CrudContext<TEntity> afterContext = CreateCrudContext(entity, CrudOperationType.Create);
        await ExecuteRulesAsync(afterContext, HookPoint.AfterRule, cancellationToken);

        MResponse<TEntity> value = new()
        {
            Result = entity
        };
        return Ok(value);
    }

/// <inheritdoc />
    [HttpPut("{id:guid}")]
    public virtual async Task<IActionResult> Update(Guid id, [FromBody] TEntity entity,
        CancellationToken cancellationToken)
    {
        licenseGuard.EnsureValid("api.update", typeof(TEntity).Name);
        // Validate input
        if (!ModelState.IsValid)
        {
            MResponse<object> response = new();
            response.SetError("ValidationFailed");
            return BadRequest(response);
        }

        // Check permission
        if (!await CheckPermissionAsync("Update", cancellationToken))
        {
            return Forbid();
        }

        // Apply tenant filter to prevent cross-tenant access
        IQueryable<TEntity> query = ApplyTenantFilter(dbContext.Set<TEntity>());
        TEntity? existing = await query.FirstOrDefaultAsync(x => x.EntityId == id && !x.IsDeleted, cancellationToken);

        if (existing == null)
        {
            MResponse<object> response = new();
            response.SetError("EntityNotFound");
            return NotFound(response);
        }

        // Execute BeforeUpdate business rules (before changes are applied)
        CrudContext<TEntity> beforeContext = CreateCrudContext(entity, CrudOperationType.Update, originalEntity: existing);
        (bool beforeSuccess, string? beforeError) = await ExecuteRulesAsync(beforeContext, HookPoint.BeforeRule, cancellationToken);
        if (!beforeSuccess)
        {
            MResponse<object> response = new();
            response.SetError(beforeError ?? "Business rule validation failed");
            return BadRequest(response);
        }

        // SECURITY: Prevent Mass Assignment attacks by excluding sensitive system fields
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> entry = dbContext.Entry(existing);
        HashSet<string> protectedProperties =
        [
            nameof(MEntity.EntityId),
            nameof(MEntity.IsDeleted),
            nameof(MEntity.CreatorUserId),
            nameof(MEntity.LastModificationUserId),
            nameof(MEntity.CreationTime),
            nameof(MEntity.LastModificationTime),
            nameof(MEntity.DeletionTime),
            nameof(MEntity.DeletedUserId),
            nameof(MEntity.Id),
            nameof(MEntity.CreatedDateTs),
            nameof(MEntity.LastModificationTimeTs),
            nameof(MEntity.DeletedDateTs),
            "TenantId" // Prevent tenant switching attack
        ];

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry property in entry.Properties)
        {
            if (!protectedProperties.Contains(property.Metadata.Name))
            {
                object? newValue = entity.GetType().GetProperty(property.Metadata.Name)?.GetValue(entity);
                if (newValue != null)
                {
                    property.CurrentValue = newValue;
                }
            }
        }

        // Update audit fields
        existing.LastModificationTime = dateTimeService.UtcNow();
        existing.LastModificationUserId = GetCurrentUserId();

        await dbContext.SaveChangesAsync(cancellationToken);

        // Execute AfterUpdate business rules
        CrudContext<TEntity> afterContext = CreateCrudContext(existing, CrudOperationType.Update);
        await ExecuteRulesAsync(afterContext, HookPoint.AfterRule, cancellationToken);

        MResponse<TEntity> value = new()
        {
            Result = existing
        };
        return Ok(value);
    }

/// <inheritdoc />
    [HttpDelete("{id:guid}")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        licenseGuard.EnsureValid("api.delete", typeof(TEntity).Name);
        // Check permission
        if (!await CheckPermissionAsync("Delete", cancellationToken))
        {
            return Forbid();
        }

        // Apply tenant filter to prevent cross-tenant deletion
        IQueryable<TEntity> query = ApplyTenantFilter(dbContext.Set<TEntity>());
        TEntity? existing = await query.FirstOrDefaultAsync(x => x.EntityId == id && !x.IsDeleted, cancellationToken);

        if (existing == null)
        {
            MResponse<object> response = new();
            response.SetError("EntityNotFound");
            return NotFound(response);
        }

        // Execute BeforeDelete business rules
        CrudContext<TEntity> beforeContext = CreateCrudContext(existing, CrudOperationType.Delete);
        (bool beforeSuccess, string? beforeError) = await ExecuteRulesAsync(beforeContext, HookPoint.BeforeRule, cancellationToken);
        if (!beforeSuccess)
        {
            MResponse<object> response = new();
            response.SetError(beforeError ?? "Business rule validation failed");
            return BadRequest(response);
        }

        // Soft delete with audit trail
        existing.IsDeleted = true;
        existing.DeletionTime = dateTimeService.UtcNow();
        existing.DeletedUserId = GetCurrentUserId();

        await dbContext.SaveChangesAsync(cancellationToken);

        // Execute AfterDelete business rules
        CrudContext<TEntity> afterContext = CreateCrudContext(existing, CrudOperationType.Delete);
        await ExecuteRulesAsync(afterContext, HookPoint.AfterRule, cancellationToken);

        return Ok(new MResponse<object>());
    }

    #region Business Rule Execution

    /// <summary>
    /// Executes business rules at the specified hook point.
    /// Returns true if rules passed, false if operation should be cancelled.
    /// </summary>
    protected virtual async Task<(bool Success, string? ErrorMessage)> ExecuteRulesAsync(
        CrudContext<TEntity> context,
        HookPoint hookPoint,
        CancellationToken cancellationToken)
    {
        // Get rule orchestrator from DI (optional)
        _ruleOrchestrator ??= HttpContext.RequestServices.GetService<RuleOrchestrator<CrudContext<TEntity>>>();

        if (_ruleOrchestrator == null)
        {
            return (true, null); // No rules registered, allow operation
        }

        // Rule orchestration is a paid feature and must be gated at runtime.
        licenseGuard.EnsureFeature(FreeTierFeatures.Premium.RuleEngine);

        try
        {
            await _ruleOrchestrator.ExecuteAsync(context, hookPoint, cancellationToken);

            // Check if rules cancelled the operation
            if (context.CancelOperation)
            {
                return (false, context.CancellationReason ?? "Operation cancelled by business rule");
            }

            // Check for validation errors
            if (context.ValidationErrors.Count > 0)
            {
                return (false, string.Join("; ", context.ValidationErrors));
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Rule execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a CRUD context for rule execution.
    /// </summary>
    protected virtual CrudContext<TEntity> CreateCrudContext(
        TEntity entity,
        CrudOperationType operationType,
        TEntity? originalEntity = null)
    {
        CrudContext<TEntity> context = new()
        {
            Entity = entity,
            OriginalEntity = originalEntity,
            OperationType = operationType,
            UserId = GetCurrentUserId(),
            TenantId = TenantContext.CurrentTenantId
        };
        return context;
    }

    #endregion

    #region Security Helpers

    /// <summary>
    /// Applies tenant filter to query if entity implements ITenantScoped
    /// </summary>
    protected virtual IQueryable<TEntity> ApplyTenantFilter(IQueryable<TEntity> query)
    {
        if (!_isTenantScoped || _tenantIdProperty == null)
        {
            return query;
        }

        if (!_multiTenantEnabled)
        {
            return query;
        }

        licenseGuard.EnsureFeature(FreeTierFeatures.Premium.MultiTenant);
        string? currentTenantId = TenantContext.CurrentTenantId;
        if (string.IsNullOrWhiteSpace(currentTenantId))
        {
            throw new MUnauthorizedException("Tenant context is required for this resource.");
        }

        // Use reflection to build the filter expression
        return query.Where(e => EF.Property<string>(e, "TenantId") == currentTenantId);
    }

    /// <summary>
    /// Sets TenantId on entity if it implements ITenantScoped
    /// </summary>
    protected virtual void SetTenantId(TEntity entity)
    {
        if (!_isTenantScoped || _tenantIdProperty == null)
        {
            return;
        }

        if (!_multiTenantEnabled)
        {
            return;
        }

        licenseGuard.EnsureFeature(FreeTierFeatures.Premium.MultiTenant);
        string? currentTenantId = TenantContext.CurrentTenantId;
        if (string.IsNullOrWhiteSpace(currentTenantId))
        {
            throw new MUnauthorizedException("Tenant context is required for this resource.");
        }

        _tenantIdProperty.SetValue(entity, currentTenantId);
    }

    /// <summary>
    /// Gets current user ID from claims
    /// </summary>
    protected virtual Guid? GetCurrentUserId()
    {
        string? userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(ClaimConstants.UserIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("user_id")?.Value;

        return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
    }

    /// <summary>
    /// Checks if current user has required permission for the operation
    /// </summary>
    protected virtual async Task<bool> CheckPermissionAsync(string operation, CancellationToken cancellationToken)
    {
        // Get permission configuration from attribute
        GenericCrudPermissionAttribute? permissionConfig = GetType().GetCustomAttribute<GenericCrudPermissionAttribute>();

        // If no attribute or skip is enabled, allow access
        if (permissionConfig == null || permissionConfig.SkipPermissionCheck)
        {
            return true;
        }

        licenseGuard.EnsureFeature(FreeTierFeatures.Premium.AdvancedAuth);

        if (_multiTenantEnabled)
        {
            string? claimTenantId = User.FindFirst(ClaimConstants.TenantId)?.Value;
            string? currentTenantId = TenantContext.CurrentTenantId;
            if (!TenantSecurityValidator.TryValidate(
                    currentTenantId,
                    claimTenantId,
                    null,
                    _requireTenantClaimForAuthenticatedUser,
                    out _))
            {
                return false;
            }
        }

        // Determine required permission
        string? requiredPermission = operation switch
        {
            "View" => permissionConfig.ReadPermission,
            "Create" => permissionConfig.CreatePermission,
            "Update" => permissionConfig.UpdatePermission,
            "Delete" => permissionConfig.DeletePermission,
            _ => null
        };

        // If specific permission not set, use prefix pattern
        if (string.IsNullOrEmpty(requiredPermission))
        {
            string prefix = permissionConfig.PermissionPrefix ?? typeof(TEntity).Name.Replace("Entity", "");
            requiredPermission = $"{prefix}.{operation}";
        }

        // Get user permissions from cache
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        IServiceProvider? services = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IServiceProvidersFeature>()
            ?.RequestServices;
        IMultiLevelCacheService? cacheService = services?.GetService<IMultiLevelCacheService>();
        List<string>? userPermissions;
        if (cacheService == null)
        {
            userPermissions = await GetUserPermissionsFromDbAsync(userId.Value, cancellationToken);
        }
        else
        {
            string cacheKey = RbacCacheKeys.UserPermissionsByEntityId(userId.Value);
            userPermissions = await cacheService.GetOrSetAsync(
                cacheKey,
                async () => await GetUserPermissionsFromDbAsync(userId.Value, cancellationToken),
                15,
                cancellationToken);

            // Backward compatibility for pre-upgrade cache entries.
            if (userPermissions == null)
            {
                List<string>? legacy = await cacheService.GetAsync<List<string>>(RbacCacheKeys.LegacyUserPermissions(userId.Value),
                    cancellationToken);
                if (legacy != null)
                {
                    userPermissions = legacy;
                    await cacheService.SetAsync(cacheKey, legacy, 15, cancellationToken);
                }
            }
        }

        return userPermissions?.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Gets user permissions from database
    /// </summary>
    private async Task<List<string>> GetUserPermissionsFromDbAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (from ur in dbContext.UserRoles.AsNoTracking()
                      join role in dbContext.Roles.AsNoTracking() on ur.RoleId equals role.EntityId
                      join user in dbContext.Users.AsNoTracking() on ur.UserId equals user.EntityId
                      join rp in dbContext.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                      join p in dbContext.Permissions.AsNoTracking() on rp.PermissionId equals p.EntityId
                      where ur.UserId == userId
                            && !ur.IsDeleted
                            && !user.IsDeleted
                            && !role.IsDeleted
                            && !rp.IsDeleted
                            && !p.IsDeleted
                      select p.Name).Distinct().ToListAsync(cancellationToken);
    }

    #endregion
}
