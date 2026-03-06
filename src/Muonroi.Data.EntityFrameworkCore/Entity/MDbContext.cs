namespace Muonroi.Data.EntityFrameworkCore.Entity;

public class MDbContext : DbContext, Muonroi.Data.Abstractions.UnitOfWork.IMUnitOfWork, IMDataContext, ITransactionalRuleContext, IIdentityAuth
{
    private readonly IMediator _mediator;
    private readonly ILogger<MDbContext>? _logger;
    private readonly ILicenseGuard? _licenseGuard;
    private readonly IMDateTimeService? _dateTimeService;

    private IDbContextTransaction? _currentTransaction;

    private readonly List<MEntity> _trackEntities = [];
    public bool HasActiveTransaction => _currentTransaction != null;

    public virtual DbSet<MRolePermission> RolePermissions { get; set; }
    public virtual DbSet<MRefreshToken> RefreshTokens { get; set; }
    public virtual DbSet<MUser> Users { get; set; }
    public virtual DbSet<MRole> Roles { get; set; }
    public virtual DbSet<MPermission> Permissions { get; set; }
    public virtual DbSet<MUserRole> UserRoles { get; set; }
    public virtual DbSet<MLanguage> Languages { get; set; }
    public virtual DbSet<MUserToken> UserTokens { get; set; }
    public virtual DbSet<MUserLoginAttempt> MUserLoginAttempts { get; set; }
    public virtual DbSet<MPermissionGroup> PermissionGroups { get; set; }
    public virtual DbSet<MPermissionAuditLog> PermissionAuditLogs { get; set; }
    public virtual DbSet<MTenantQuota> TenantQuotas { get; set; }
    public virtual DbSet<MTenantQuotaUsage> TenantQuotaUsages { get; set; }
    public virtual DbSet<MWebAuthnCredential> WebAuthnCredentials { get; set; }

    public MDbContext(DbContextOptions options, IMediator mediator, ILicenseGuard? licenseGuard = null, ILogger<MDbContext>? logger = null, IMDateTimeService? dateTimeService = null)
        : base(options)
    {
        _mediator = mediator;
        _logger = logger;
        _licenseGuard = licenseGuard;
        _dateTimeService = dateTimeService;
        Debug.WriteLine("BaseDbContext::ctor ->" + GetHashCode());
    }

    public sealed override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public IDbContextTransaction? GetCurrentTransaction()
    {
        return _currentTransaction;
    }

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

    public async Task<Guid> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
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
        DateTime utcNow = _dateTimeService?.UtcNow() ?? DateTime.UtcNow; // MBB001-exempt: fallback for subclass compat when IMDateTimeService not injected

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
                        item.State = EntityState.Added;
                    }

                    break;

                case EntityState.Modified:
                    Entry(item.Entity).Property("Id").IsModified = false;
                    if (item.Entity is MEntity modifiedEntity)
                    {
                        modifiedEntity.LastModificationTime = utcNow;
                        modifiedEntity.LastModificationTimeTs = utcNow.GetTimeStamp();
                        item.State = EntityState.Modified;
                    }

                    break;

                case EntityState.Deleted:
                    if (item.Entity is MEntity deletedEntity)
                    {
                        deletedEntity.IsDeleted = true;
                        deletedEntity.DeletionTime = utcNow;
                        deletedEntity.DeletedDateTs = utcNow.GetTimeStamp();
                        item.State = EntityState.Modified;
                    }

                    break;
            }
        }
    }

    private async Task DispatchDomainEventsAsync()
    {
        IEnumerable<MEntity> domainEntities = _trackEntities
            .Where(x => x.DomainEvents is { Count: > 0 })
            .Distinct();

        MEntity[] mEntities = domainEntities as MEntity[] ?? [.. domainEntities];
        List<IMDomainEvent> domainEvents = [.. mEntities.SelectMany(x => x.DomainEvents)];

        mEntities.ToList().ForEach(entity => entity.ClearDomainEvents());

        IEnumerable<Task> tasks = domainEvents.Select(async domainEvent =>
        {
            Console.WriteLine($"Dispatching InternalEvent: {domainEvent.GetType()}");
            await _mediator.Publish((Mediator.Mediator.Interfaces.INotification)domainEvent);
            Console.WriteLine($"Dispatched InternalEvent: {domainEvent.GetType()}");
        });

        await Task.WhenAll(tasks);
        _logger?.LogInformation("Dispatched {Count} domain events successfully", domainEvents.Count);
        _trackEntities.Clear();
    }

    public async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            return null;
        }

        _currentTransaction = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);
        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(IDbContextTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction != _currentTransaction)
        {
            throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseUtcDateTime();

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
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"[WARNING] Architecture Violation: Entity '{entityType.ClrType.Name}' SHOULD inherit from '{nameof(MEntity)}'.");
                Console.WriteLine(
                    $"          - Benefit: Auto Audit, Soft-Delete, Snowflake ID, Multi-Tenant Security.");
                Console.WriteLine(
                    $"          - Guide: https://github.com/muonroi/MuonroiBuildingBlock/blob/main/docs/backend-guide.md#1-entity");
                Console.ForegroundColor = originalColor;
            }

            LambdaExpression? combinedFilter = entityType.GetQueryFilter();

            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                PropertyInfo? tenantProp = entityType.ClrType.GetProperty(nameof(ITenantScoped.TenantId));
                if (tenantProp != null && tenantProp.PropertyType == typeof(string))
                {
                    LambdaExpression tenantFilter = BuildTenantFilter(entityType.ClrType, tenantProp);
                    combinedFilter = CombineWithAnd(combinedFilter, tenantFilter, entityType.ClrType);
                    modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ITenantScoped.TenantId));
                }
            }

            if (typeof(MEntity).IsAssignableFrom(entityType.ClrType))
            {
                PropertyInfo? creatorProp = entityType.ClrType.GetProperty("CreatorUserId");
                if (creatorProp != null && creatorProp.PropertyType == typeof(Guid))
                {
                    LambdaExpression creatorFilter = BuildCreatorFilter(entityType.ClrType, creatorProp);
                    combinedFilter = CombineWithAnd(combinedFilter, creatorFilter, entityType.ClrType);
                }
            }

            if (combinedFilter != null)
            {
                entityType.SetQueryFilter(combinedFilter);
            }
        }
    }

    private static LambdaExpression BuildTenantFilter(Type entityType, PropertyInfo tenantProp)
    {
        ParameterExpression parameter = Expression.Parameter(entityType, "e");
        MemberExpression propertyAccess = Expression.Property(parameter, tenantProp);
        MemberExpression currentTenant = Expression.Property(null, typeof(TenantContext), nameof(TenantContext.CurrentTenantId));
        BinaryExpression isCurrentNull = Expression.Equal(currentTenant, Expression.Constant(null, typeof(string)));
        BinaryExpression isMatch = Expression.Equal(propertyAccess, currentTenant);
        BinaryExpression body = Expression.OrElse(isMatch, isCurrentNull);
        return Expression.Lambda(body, parameter);
    }

    private static LambdaExpression BuildCreatorFilter(Type entityType, PropertyInfo creatorProp)
    {
        ParameterExpression parameter = Expression.Parameter(entityType, "e");
        MemberExpression propAccess = Expression.Property(parameter, creatorProp);
        MemberExpression currentUserId = Expression.Property(null, typeof(UserContext), nameof(UserContext.CurrentUserGuid));

        MethodInfo? toString = typeof(Guid).GetMethod("ToString", Type.EmptyTypes);
        MethodCallExpression guidString = Expression.Call(propAccess, toString!);

        BinaryExpression isNull = Expression.Equal(currentUserId, Expression.Constant(null, typeof(string)));
        BinaryExpression isEqual = Expression.Equal(guidString, currentUserId);
        BinaryExpression body = Expression.OrElse(isNull, isEqual);
        return Expression.Lambda(body, parameter);
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
