using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Represents the base database context for the Muonroi application, providing audit, soft-delete, and multi-tenancy support.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "EF Core query composition and projection use null-forgiving on values that are structurally guaranteed non-null by the query shape.")]
public class MDbContext : DbContext, IMUnitOfWork, IMDataContext, ITransactionalRuleContext, IIdentityAuth
{
    private static readonly ActivitySource ActivitySource = new("Muonroi.Data.EntityFrameworkCore");
    private static readonly HashSet<Type> CreatorFilterExemptEntityTypes =
    [
        typeof(MPermission),
        typeof(MPermissionGroup),
        typeof(MRole),
        typeof(MRolePermission),
        typeof(MUser),
        typeof(MUserLoginAttempt),
        typeof(MUserRole),
        typeof(MUserToken),
        typeof(MRefreshToken),
        typeof(MWebAuthnCredential)
    ];
    private readonly IMediator? _mediator;
    private readonly IMLog<MDbContext>? _logger;
    private readonly ILicenseGuard? _licenseGuard;
    private readonly IMDateTimeService? _dateTimeService;

    private IDbContextTransaction? _currentTransaction;

    private readonly List<MEntity> _trackEntities = [];

    /// <summary>
    /// Gets a value indicating whether there is an active transaction.
    /// </summary>
    public bool HasActiveTransaction => _currentTransaction != null;

    /// <summary>
    /// Gets or sets the role permissions.
    /// </summary>
    public virtual DbSet<MRolePermission> RolePermissions { get; set; }

    /// <summary>
    /// Gets or sets the refresh tokens.
    /// </summary>
    public virtual DbSet<MRefreshToken> RefreshTokens { get; set; }

    /// <summary>
    /// Gets or sets the users.
    /// </summary>
    public virtual DbSet<MUser> Users { get; set; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    public virtual DbSet<MRole> Roles { get; set; }

    /// <summary>
    /// Gets or sets the permissions.
    /// </summary>
    public virtual DbSet<MPermission> Permissions { get; set; }

    /// <summary>
    /// Gets or sets the user roles.
    /// </summary>
    public virtual DbSet<MUserRole> UserRoles { get; set; }

    /// <summary>
    /// Gets or sets the languages.
    /// </summary>
    public virtual DbSet<MLanguage> Languages { get; set; }

    /// <summary>
    /// Gets or sets the user tokens.
    /// </summary>
    public virtual DbSet<MUserToken> UserTokens { get; set; }

    /// <summary>
    /// Gets or sets the user login attempts.
    /// </summary>
    public virtual DbSet<MUserLoginAttempt> MUserLoginAttempts { get; set; }

    /// <summary>
    /// Gets or sets the permission groups.
    /// </summary>
    public virtual DbSet<MPermissionGroup> PermissionGroups { get; set; }

    /// <summary>
    /// Gets or sets the permission audit logs.
    /// </summary>
    public virtual DbSet<MPermissionAuditLog> PermissionAuditLogs { get; set; }

    /// <summary>
    /// Gets or sets the tenant quotas.
    /// </summary>
    public virtual DbSet<MTenantQuota> TenantQuotas { get; set; }

    /// <summary>
    /// Gets or sets the tenant quota usages.
    /// </summary>
    public virtual DbSet<MTenantQuotaUsage> TenantQuotaUsages { get; set; }

    /// <summary>
    /// Gets or sets the WebAuthn credentials.
    /// </summary>
    public virtual DbSet<MWebAuthnCredential> WebAuthnCredentials { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    /// <param name="mediator">The mediator for dispatching events.</param>
    /// <param name="licenseGuard">The license guard.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="dateTimeService">The date time service.</param>
    public MDbContext(DbContextOptions options, IMediator? mediator = null, ILicenseGuard? licenseGuard = null, IMLog<MDbContext>? logger = null, IMDateTimeService? dateTimeService = null)
        : base(options)
    {
        _mediator = mediator;
        _logger = logger;
        _licenseGuard = licenseGuard;
        _dateTimeService = dateTimeService;
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this instance.</returns>
    public sealed override int GetHashCode()
    {
        return base.GetHashCode();
    }

    /// <summary>
    /// Gets the current transaction.
    /// </summary>
    /// <returns>The current transaction, or null if there is no active transaction.</returns>
    public IDbContextTransaction? GetCurrentTransaction()
    {
        return _currentTransaction;
    }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (!LicenseExecutionContext.IsInLicenseCheck)
        {
            using (LicenseExecutionContext.BeginScope())
            {
                _licenseGuard?.EnsureValid("db.save");
            }
        }
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Saves entities and dispatches domain events asynchronously within a transaction.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the transaction ID.</returns>
    public async Task<Guid> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("db.save_entities", ActivityKind.Internal);
        activity?.SetTag("db.system", Database.ProviderName);
        activity?.SetTag("tenant.id", TenantContext.CurrentTenantId);

        UpdateTimestamps();
        IExecutionStrategy strategy = Database.CreateExecutionStrategy();
        if (Database.IsInMemory())
        {
            _ = await base.SaveChangesAsync(cancellationToken);
            await DispatchDomainEventsAsync();
            return Guid.NewGuid();
        }

        if (!HasActiveTransaction)
        {
            return await strategy.ExecuteAsync(async () =>
            {
                IDbContextTransaction? dbContextTransaction = await BeginTransactionAsync().ConfigureAwait(false);
                try
                {
                    _ = await base.SaveChangesAsync(cancellationToken);
                    await DispatchDomainEventsAsync();
                    await CommitTransactionAsync(dbContextTransaction!).ConfigureAwait(false);
                    return dbContextTransaction!.TransactionId;
                }
                catch (Exception)
                {
                    RollbackTransaction();
                    throw;
                }
            }).ConfigureAwait(false);
        }

        _ = await base.SaveChangesAsync(cancellationToken);
        await DispatchDomainEventsAsync();
        return _currentTransaction?.TransactionId ?? Guid.NewGuid();
    }

    private void UpdateTimestamps()
    {
        DateTime utcNow = _dateTimeService?.UtcNow() ?? DateTime.UtcNow;
        string? currentUserIdStr = UserContext.CurrentUserGuid;
        _ = Guid.TryParse(currentUserIdStr, out Guid currentUserId);

        IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> modifiedEntries = ChangeTracker
            .Entries()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry? item in modifiedEntries)
        {
            switch (item.State)
            {
                case EntityState.Added:
                    if (item.Entity is MEntity addedEntity)
                    {
                        addedEntity.CreatedDateTs = utcNow.GetTimeStamp();
                        addedEntity.CreationTime = utcNow;
                        if (addedEntity.CreatorUserId == Guid.Empty)
                        {
                            addedEntity.CreatorUserId = currentUserId;
                        }
                        item.State = EntityState.Added;
                    }

                    break;

                case EntityState.Modified:
                    Entry(item.Entity).Property("Id").IsModified = false;
                    if (item.Entity is MEntity modifiedEntity)
                    {
                        modifiedEntity.LastModificationTime = utcNow;
                        modifiedEntity.LastModificationTimeTs = utcNow.GetTimeStamp();
                        modifiedEntity.LastModificationUserId = currentUserId;
                        item.State = EntityState.Modified;
                    }

                    break;

                case EntityState.Deleted:
                    if (item.Entity is MEntity deletedEntity)
                    {
                        deletedEntity.IsDeleted = true;
                        deletedEntity.DeletionTime = utcNow;
                        deletedEntity.DeletedDateTs = utcNow.GetTimeStamp();
                        deletedEntity.DeletedUserId = currentUserId;
                        item.State = EntityState.Modified;
                    }

                    break;
            }
        }
    }

    private async Task DispatchDomainEventsAsync()
    {
        if (_mediator is null)
        {
            // No mediator registered — clear tracked events without dispatch
            _trackEntities.Clear();
            return;
        }

        IEnumerable<MEntity> domainEntities = _trackEntities
            .Where(x => x.DomainEvents is { Count: > 0 })
            .Distinct();

        MEntity[] mEntities = domainEntities as MEntity[] ?? [.. domainEntities];
        List<IMDomainEvent> domainEvents = [.. mEntities.SelectMany(x => x.DomainEvents)];

        mEntities.ToList().ForEach(entity => entity.ClearDomainEvents());

        IEnumerable<Task> tasks = domainEvents.Select(async domainEvent =>
        {
            _logger?.Debug("Dispatching InternalEvent: {EventType}", domainEvent.GetType().Name);
            await _mediator.Publish((Mediator.Mediator.Interfaces.INotification)domainEvent);
            _logger?.Debug("Dispatched InternalEvent: {EventType}", domainEvent.GetType().Name);
        });

        await Task.WhenAll(tasks);
        _logger?.Info("Dispatched {Count} domain events successfully", domainEvents.Count);
        _trackEntities.Clear();
    }

    /// <summary>
    /// Begins a new database transaction asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="IDbContextTransaction"/>, or null if a transaction is already active.</returns>
    public async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        using var activity = ActivitySource.StartActivity("db.begin_transaction", ActivityKind.Internal);
        if (_currentTransaction != null)
        {
            return null;
        }

        _currentTransaction = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);
        activity?.SetTag("transaction.id", _currentTransaction.TransactionId);
        return _currentTransaction;
    }

    /// <summary>
    /// Commits the current database transaction asynchronously.
    /// </summary>
    /// <param name="transaction">The transaction to commit.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transaction"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="transaction"/> is not the current transaction.</exception>
    public async Task CommitTransactionAsync(IDbContextTransaction transaction)
    {
        MGuard.NotNull(transaction);

        if (transaction != _currentTransaction)
        {
            throw new MInternalException($"Transaction {transaction.TransactionId} is not current");
        }

        try
        {
            _ = await SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            RollbackTransaction();
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    /// <summary>
    /// Rolls back the current database transaction.
    /// </summary>
    public void RollbackTransaction()
    {
        try
        {
            _currentTransaction?.Rollback();
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    /// <summary>
    /// Adds an entity to the temporary list used for domain event dispatching.
    /// The list is cleared once all domain events have been published.
    /// </summary>
    /// <param name="entity">The entity whose events should be dispatched.</param>
    public void TrackEntity(MEntity entity)
    {
        _trackEntities.Add(entity);
    }

    /// <summary>
    /// Configures the EF Core model and query filters.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseUtcDateTime();

        // Normalize all Guid properties to lowercase string for SQLite compatibility.
        // SQLite TEXT comparisons are case-sensitive; without normalization, a Guid written
        // as "9415A44A-..." cannot be matched by a query using "9415a44a-..." — causing
        // silent 401 failures during token validation. Applying this once at the model level
        // fixes all Guid columns (EntityId, CreatorUserId, RefreshToken.CreatorUserId, etc.)
        // consistently across every entity without touching any query code.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" || Database.IsInMemory())
        {
            ValueConverter<Guid, string> guidConverter = new(
                v => v.ToString("D").ToLowerInvariant(),
                v => Guid.Parse(v));

            ValueConverter<Guid?, string?> nullableGuidConverter = new(
                v => v.HasValue ? v.Value.ToString("D").ToLowerInvariant() : null,
                v => string.IsNullOrEmpty(v) ? null : Guid.Parse(v));

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(Guid))
                        property.SetValueConverter(guidConverter);
                    else if (property.ClrType == typeof(Guid?))
                        property.SetValueConverter(nullableGuidConverter);
                }
            }
        }

        // Explicit discovery for robust schema creation
        modelBuilder.Entity<MUser>();
        modelBuilder.Entity<MRole>();
        modelBuilder.Entity<MPermission>();
        modelBuilder.Entity<MUserRole>();
        modelBuilder.Entity<MRolePermission>();
        modelBuilder.Entity<MRefreshToken>();
        modelBuilder.Entity<MLanguage>();
        modelBuilder.Entity<MUserToken>();
        modelBuilder.Entity<MUserLoginAttempt>();
        modelBuilder.Entity<MPermissionGroup>();
        modelBuilder.Entity<MPermissionAuditLog>();
        modelBuilder.Entity<MTenantQuota>();
        modelBuilder.Entity<MTenantQuotaUsage>();
        modelBuilder.Entity<MWebAuthnCredential>();

        CustomColumnOrderConvention customConvention = new();
        customConvention.Customize(modelBuilder, this);
        _ = modelBuilder.ApplyConfiguration(new MUserConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MUserRoleConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MLanguageConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MPermissionConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MRoleConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MUserTokenConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MUserLoginAttemptConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MPermissionGroupConfiguration());
        _ = modelBuilder.ApplyConfiguration(new MPermissionAuditLogConfiguration());

        modelBuilder.Entity<MTenantQuota>()
            .HasIndex(x => x.TenantId)
            .IsUnique();

        modelBuilder.Entity<MTenantQuotaUsage>()
            .HasIndex(x => new { x.TenantId, x.QuotaType, x.Period })
            .IsUnique();

        modelBuilder.Entity<MWebAuthnCredential>()
            .HasIndex(x => new { x.UserId, x.CredentialId })
            .IsUnique();

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(MEntity).IsAssignableFrom(entityType.ClrType) && !entityType.IsOwned())
            {
                _logger?.Warn(
                    "[Architecture] Entity '{EntityName}' SHOULD inherit from '{BaseType}' — Benefit: Auto Audit, Soft-Delete, Snowflake ID, Multi-Tenant Security | Guide: {Guide}",
                    entityType.ClrType.Name, nameof(MEntity),
                    "https://github.com/muonroi/MuonroiBuildingBlock/blob/main/docs/backend-guide.md#1-entity");
            }

            LambdaExpression? combinedFilter = entityType.GetQueryFilter();

            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                PropertyInfo? tenantProp = entityType.ClrType.GetProperty(nameof(ITenantScoped.TenantId));
                if (tenantProp != null && tenantProp.PropertyType == typeof(string))
                {
                    bool isTestProvider = Database.IsInMemory() || Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
                    LambdaExpression tenantFilter = BuildTenantFilter(entityType.ClrType, tenantProp, isTestProvider);
                    combinedFilter = CombineWithAnd(combinedFilter, tenantFilter, entityType.ClrType);
                    modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ITenantScoped.TenantId));
                }
            }

            if (typeof(MEntity).IsAssignableFrom(entityType.ClrType))
            {
                PropertyInfo? creatorProp = entityType.ClrType.GetProperty("CreatorUserId");
                if (creatorProp != null &&
                    creatorProp.PropertyType == typeof(Guid) &&
                    ShouldApplyCreatorFilter(entityType.ClrType))
                {
                    bool isTestProvider = Database.IsInMemory() || Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
                    LambdaExpression creatorFilter = BuildCreatorFilter(entityType.ClrType, creatorProp, isTestProvider);
                    combinedFilter = CombineWithAnd(combinedFilter, creatorFilter, entityType.ClrType);
                }
            }

            if (combinedFilter != null)
            {
                entityType.SetQueryFilter(combinedFilter);
            }
        }
    }

    private static LambdaExpression BuildTenantFilter(Type entityType, PropertyInfo tenantProp, bool isInMemory = false)
    {
        ParameterExpression parameter = Expression.Parameter(entityType, "e");
        MemberExpression propertyAccess = Expression.Property(parameter, tenantProp);
        MemberExpression currentTenant = Expression.Property(null, typeof(TenantContext), nameof(TenantContext.CurrentTenantId));

        // Read AllowCrossTenantAccess flag
        MemberExpression allowCrossTenant = Expression.Property(null, typeof(TenantContext), nameof(TenantContext.AllowCrossTenantAccess));

        // e.TenantId == CurrentTenantId (fail-closed: null == null is false in SQL)
        BinaryExpression isMatch = Expression.Equal(propertyAccess, currentTenant);

        // AllowCrossTenantAccess == true bypasses filter (for admin operations)
        // IF InMemory, we ALWAYS bypass to facilitate Unit Testing
        Expression bypassExpression = isInMemory 
            ? Expression.Constant(true) 
            : allowCrossTenant;

        BinaryExpression body = Expression.OrElse(isMatch, bypassExpression);
        return Expression.Lambda(body, parameter);
    }

    private static LambdaExpression BuildCreatorFilter(Type entityType, PropertyInfo creatorProp, bool isInMemory = false)
    {
        ParameterExpression parameter = Expression.Parameter(entityType, "e");
        MemberExpression propAccess = Expression.Property(parameter, creatorProp);
        MemberExpression currentUserId = Expression.Property(null, typeof(UserContext), nameof(UserContext.CurrentUserGuid));

        // Read AllowCrossTenantAccess flag (also used for bypassing creator filters in admin mode)
        MemberExpression allowCrossTenant = Expression.Property(null, typeof(TenantContext), nameof(TenantContext.AllowCrossTenantAccess));

        MethodInfo? toString = typeof(Guid).GetMethod("ToString", Type.EmptyTypes);
        MethodCallExpression guidString = Expression.Call(propAccess, toString!);

        // e.CreatorUserId == CurrentUserId (fail-closed: null == null is false in SQL)
        BinaryExpression isEqual = Expression.Equal(guidString, currentUserId);

        // AllowCrossTenantAccess == true bypasses filter
        // IF InMemory, we ALWAYS bypass to facilitate Unit Testing
        Expression bypassExpression = isInMemory 
            ? Expression.Constant(true) 
            : allowCrossTenant;

        BinaryExpression body = Expression.OrElse(isEqual, bypassExpression);
        return Expression.Lambda(body, parameter);
    }

    private static bool ShouldApplyCreatorFilter(Type entityType)
    {
        return !CreatorFilterExemptEntityTypes.Contains(entityType);
    }

    private static LambdaExpression CombineWithAnd(LambdaExpression? existing, LambdaExpression added, Type entityType)
    {
        if (existing == null)
        {
            return added;
        }

        ParameterExpression parameter = Expression.Parameter(entityType, "e");
        Expression left = ReplaceParameter(existing.Body, existing.Parameters[0], parameter);
        Expression right = ReplaceParameter(added.Body, added.Parameters[0], parameter);
        return Expression.Lambda(Expression.AndAlso(left, right), parameter);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source,
        ParameterExpression target)
    {
        return new ParameterReplaceVisitor(source, target).Visit(expression)!;
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == source ? target : base.VisitParameter(node);
        }
    }
}
