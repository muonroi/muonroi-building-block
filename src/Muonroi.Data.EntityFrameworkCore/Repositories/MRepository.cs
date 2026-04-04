using System.Data;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Provides a base implementation for a repository of entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the entity. Must inherit from <see cref="MEntity"/>.</typeparam>
public class MRepository<T> : IMRepository<T> where T : MEntity
{
    private readonly IAuthenticateInfoContext _authContext;
    private readonly ILicenseGuard _licenseGuard;
    private readonly IMDateTimeService _dateTimeService;
    private readonly IMLog<MRepository<T>>? _logger;
    /// <summary>
    /// The base database context.
    /// </summary>
    protected readonly MDbContext DbBaseContext;
    /// <summary>
    /// The database set for the entity type.
    /// </summary>
    protected readonly DbSet<T> DbSet;

    /// <summary>
    /// Gets the current user ID.
    /// </summary>
    public string? CurrentUserId => _authContext?.CurrentUserGuid;
    /// <summary>
    /// Gets the current username.
    /// </summary>
    public string? CurrentUsername => _authContext?.CurrentUsername;
    /// <summary>
    /// Gets the unit of work.
    /// </summary>
    public IMUnitOfWork UnitOfWork => DbBaseContext;

    /// <summary>
    /// Gets a queryable for the entity type, filtering out deleted entities and checking license.
    /// </summary>
    protected IQueryable<T> Queryable
    {
        get
        {
            _licenseGuard.EnsureValid("db.query", typeof(T).Name);
            return DbSet.Where(m => !m.IsDeleted);
        }
    }

    /// <summary>
    /// Gets the current tenant ID.
    /// </summary>
    protected static string? TenantId => TenantContext.CurrentTenantId;

    private Guid CurrentUserGuid => string.IsNullOrEmpty(_authContext?.CurrentUserGuid)
        ? Guid.Empty
        : Guid.Parse(_authContext.CurrentUserGuid);

    /// <summary>
    /// Initializes a new instance of the <see cref="MRepository{T}"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="authContext">The authentication information context.</param>
    /// <param name="licenseGuard">The license guard.</param>
    /// <param name="dateTimeService">The date time service.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the parameters are null.</exception>
    public MRepository(MDbContext dbContext, IAuthenticateInfoContext authContext, ILicenseGuard licenseGuard, IMDateTimeService dateTimeService, IMLog<MRepository<T>>? logger = null)
    {
        _authContext = MGuard.NotNull(authContext);
        DbBaseContext = MGuard.NotNull(dbContext);
        _licenseGuard = MGuard.NotNull(licenseGuard);
        _dateTimeService = MGuard.NotNull(dateTimeService);
        _logger = logger;
        DbSet = DbBaseContext.Set<T>();
    }

    private void Protect(string action)
    {
        _licenseGuard.EnsureValid("db." + action, typeof(T).Name);
    }

    #region Helper Methods for Timestamp Updates

    private void UpdateTimestampsForAdd(T entity, DateTime utcNow)
    {
        entity.CreatedDateTs = utcNow.GetTimeStamp(true);
        entity.CreationTime = utcNow;
        entity.LastModificationTimeTs = utcNow.GetTimeStamp(true);
        entity.CreatorUserId = CurrentUserGuid;
    }

    private void UpdateTimestampsForUpdate(T entity, DateTime utcNow)
    {
        entity.LastModificationTime = utcNow;
        entity.LastModificationTimeTs = utcNow.GetTimeStamp(true);
        entity.LastModificationUserId = CurrentUserGuid;
    }

    #endregion

    /// <summary>
    /// Adds a batch of entities asynchronously.
    /// </summary>
    /// <param name="newEntities">The entities to add.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    public virtual async Task<int> AddBatchAsync(IEnumerable<T>? newEntities)
    {
        Protect("add_batch");
        if (newEntities == null || !newEntities.Any())
        {
            return 0;
        }

        DateTime utcNow = _dateTimeService.UtcNow();

        List<T> entityList = [.. newEntities];

        foreach (T entity in entityList)
        {
            UpdateTimestampsForAdd(entity, utcNow);
            entity.EntityId = Guid.NewGuid();
            DbBaseContext.TrackEntity(entity);
        }

        if (entityList.Count > 0)
        {
            entityList[0].AddDomainEvent(new MEntitiesCreatedEvent<T>(entityList));
        }

        await DbSet.AddRangeAsync(entityList).ConfigureAwait(false);

        return await DbBaseContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Adds or updates a batch of entities asynchronously.
    /// </summary>
    /// <param name="newEntities">The entities to add or update.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    public virtual async Task<int> AddOrUpdateBatchAsync(IEnumerable<T>? newEntities)
    {
        if (newEntities == null || !newEntities.Any())
        {
            return 0;
        }

        DateTime utcNow = _dateTimeService.UtcNow();

        List<Guid> entityIdList =
        [
            .. newEntities
                .Where(e => e.EntityId != Guid.Empty)
                .Select(e => e.EntityId)
                .Distinct()
        ];

        Dictionary<Guid, T> existingEntitiesDict = await DbSet
            .Where(e => entityIdList.Contains(e.EntityId))
            .ToDictionaryAsync(e => e.EntityId)
            .ConfigureAwait(false);

        List<T> entityList = [.. newEntities];
        List<T> createdList = [];
        List<T> updatedList = [];

        foreach (T entity in entityList)
        {
            entity.LastModificationTimeTs = utcNow.GetTimeStamp(true);
            entity.CreatorUserId = CurrentUserGuid;
            if (existingEntitiesDict.TryGetValue(entity.EntityId, out T? existingEntity))
            {
                long backupId = existingEntity.Id;
                entity.Id = existingEntity.Id;
                entity.CreatedDateTs = existingEntity.CreatedDateTs;
                entity.CreationTime = existingEntity.CreationTime;
                entity.CreatorUserId = existingEntity.CreatorUserId;
                DbBaseContext.Entry(existingEntity).CurrentValues.SetValues(entity);
                existingEntity.Id = backupId;

                existingEntity.LastModificationTimeTs = utcNow.GetTimeStamp(true);
                existingEntity.LastModificationUserId = entity.CreatorUserId;

                existingEntity.AddDomainEvent(new MEntityChangedEvent<T>(existingEntity));
                DbBaseContext.TrackEntity(existingEntity);
                updatedList.Add(existingEntity);
            }
            else
            {
                UpdateTimestampsForAdd(entity, utcNow);
                entity.EntityId = Guid.NewGuid();
                DbBaseContext.TrackEntity(entity);
                await DbSet.AddAsync(entity).ConfigureAwait(false);
                createdList.Add(entity);
            }
        }

        if (createdList.Count > 0)
        {
            createdList[0].AddDomainEvent(new MEntitiesCreatedEvent<T>(createdList));
        }

        if (updatedList.Count > 0)
        {
            updatedList[0].AddDomainEvent(new MEntitiesChangedEvent<T>(updatedList));
        }

        return await DbBaseContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a batch of entities based on a predicate and an update action.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <param name="updateAction">The action to update each entity.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    public virtual async Task<int> UpdateBatchAsync(Expression<Func<T, bool>> predicate, Action<T> updateAction)
    {
        List<T> entities = await Queryable.Where(predicate)
            .ToListAsync()
            .ConfigureAwait(false);

        if (entities == null || entities.Count == 0)
        {
            return 0;
        }

        DateTime utcNow = _dateTimeService.UtcNow();

        List<T> changedList = [];

        foreach (T? entity in entities)
        {
            updateAction(entity);

            UpdateTimestampsForUpdate(entity, utcNow);

            DbBaseContext.TrackEntity(entity);
            changedList.Add(entity);
        }

        if (changedList.Count > 0)
        {
            changedList[0].AddDomainEvent(new MEntitiesChangedEvent<T>(changedList));
        }

        return await DbBaseContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a batch of entities based on a predicate (soft-delete).
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    public virtual async Task<int> DeleteBatchAsync(Expression<Func<T, bool>> predicate)
    {
        List<T> entities = await Queryable.Where(predicate)
            .ToListAsync()
            .ConfigureAwait(false);

        if (entities == null || entities.Count == 0)
        {
            return 0;
        }

        DateTime utcNow = _dateTimeService.UtcNow();
        string? currentUserGuid = string.IsNullOrEmpty(_authContext?.CurrentUserGuid)
            ? null
            : _authContext?.CurrentUserGuid;

        List<T> deletedList = [];

        foreach (T? entity in entities)
        {
            entity.IsDeleted = true;
            entity.DeletedDateTs = utcNow.GetTimeStamp(true);
            entity.DeletionTime = utcNow;
            entity.DeletedUserId = Guid.Parse(currentUserGuid ?? Guid.Empty.ToString());
            DbBaseContext.TrackEntity(entity);
            deletedList.Add(entity);
        }

        if (deletedList.Count > 0)
        {
            deletedList[0].AddDomainEvent(new MEntitiesDeletedEvent<T>(deletedList));
        }

        return await DbBaseContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a batch of entities (soft-delete).
    /// </summary>
    /// <param name="deleteEntities">The entities to delete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    public virtual async Task<int> DeleteBatchAsync(IEnumerable<T>? deleteEntities)
    {
        if (deleteEntities == null || !deleteEntities.Any())
        {
            return 0;
        }

        DateTime utcNow = _dateTimeService.UtcNow();
        List<Guid> entityIds = [.. deleteEntities.Select(e => e.EntityId)];

        List<T> existingEntities = await DbSet
            .Where(e => entityIds.Contains(e.EntityId))
            .ToListAsync()
            .ConfigureAwait(false);

        List<T> deletedList = [];

        foreach (T entity in deleteEntities)
        {
            entity.DeletedDateTs = utcNow.GetTimeStamp(true);
            entity.DeletedUserId = CurrentUserGuid;
            entity.IsDeleted = true;
            entity.DeletionTime = utcNow;

            T? existingEntity = existingEntities.FirstOrDefault(e => e.EntityId == entity.EntityId);
            if (existingEntity != null)
            {
                long backupId = existingEntity.Id;
                entity.Id = backupId;
                DbBaseContext.Entry(existingEntity).CurrentValues.SetValues(entity);
                existingEntity.Id = backupId;
                existingEntity.DeletedDateTs = utcNow.GetTimeStamp(true);
                existingEntity.DeletedUserId = entity.CreatorUserId;
                entity.AddDomainEvent(new MEntityDeletedEvent<T>(entity));
                DbBaseContext.TrackEntity(existingEntity);
                deletedList.Add(existingEntity);
            }
            else
            {
                DbBaseContext.TrackEntity(entity);
                deletedList.Add(entity);
            }
        }

        if (deletedList.Count > 0)
        {
            deletedList[0].AddDomainEvent(new MEntitiesDeletedEvent<T>(deletedList));
        }

        return await DbBaseContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    /// <param name="newEntity">The entity to add.</param>
    /// <returns>The added entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity already exists in the context.</exception>
    public virtual T Add(T newEntity)
    {
        Protect("add");
        T? existingEntity = DbBaseContext.Set<T>().Local
            .FirstOrDefault(e => e.EntityId == newEntity.EntityId);

        if (existingEntity != null)
        {
            throw new MInternalException("Entity already exists in the context.");
        }

        DateTime utcNow = _dateTimeService.UtcNow();
        UpdateTimestampsForAdd(newEntity, utcNow);
        newEntity.EntityId = Guid.NewGuid();
        newEntity.AddDomainEvent(new MEntityCreatedEvent<T>(newEntity));
        DbBaseContext.TrackEntity(newEntity);
        return DbSet.Add(newEntity).Entity;
    }

    /// <summary>
    /// Deletes an entity asynchronously (soft-delete).
    /// </summary>
    /// <param name="deleteEntity">The entity to delete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is always true.</returns>
    public virtual Task<bool> DeleteAsync(T deleteEntity)
    {
        T? existingEntity = DbBaseContext.Set<T>().Local
            .FirstOrDefault(e => e.EntityId == deleteEntity.EntityId);

        if (existingEntity != null)
        {
            DbBaseContext.Entry(existingEntity).CurrentValues.SetValues(deleteEntity);
        }
        else
        {
            DbSet.Attach(deleteEntity);
        }

        DateTime utcNow = _dateTimeService.UtcNow();
        deleteEntity.DeletionTime = utcNow;
        deleteEntity.IsDeleted = true;
        deleteEntity.DeletedDateTs = utcNow.GetTimeStamp(true);
        deleteEntity.DeletedUserId = string.IsNullOrEmpty(_authContext?.CurrentUserGuid)
            ? null
            : CurrentUserGuid;
        deleteEntity.AddDomainEvent(new MEntityDeletedEvent<T>(deleteEntity));

        DbBaseContext.Entry(deleteEntity).State = EntityState.Modified;
        return Task.FromResult(true);
    }

    /// <summary>
    /// Updates an entity asynchronously.
    /// </summary>
    /// <param name="updateEntity">The entity to update.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    public virtual async Task<int> UpdateAsync(T? updateEntity)
    {
        if (updateEntity == null)
        {
            return 0;
        }

        DateTime utcNow = _dateTimeService.UtcNow();
        UpdateTimestampsForUpdate(updateEntity, utcNow);

        T? existingEntity = await DbSet.FirstOrDefaultAsync(x => x.EntityId == updateEntity.EntityId)
            .ConfigureAwait(false);
        if (existingEntity != null)
        {
            long entityId = existingEntity.Id;
            updateEntity.Id = existingEntity.Id;
            DbBaseContext.Entry(existingEntity).CurrentValues.SetValues(updateEntity);
            existingEntity.Id = entityId;

            existingEntity.LastModificationTimeTs = utcNow.GetTimeStamp(true);
            existingEntity.LastModificationUserId = updateEntity.CreatorUserId;
            existingEntity.LastModificationTime = utcNow;
            DbBaseContext.TrackEntity(existingEntity);
        }

        return await DbBaseContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an action within a transaction asynchronously.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task ExecuteTransactionAsync(Func<Task<Muonroi.Core.Abstractions.Response.MVoidMethodResult>> action)
    {
        if (DbBaseContext.Database.IsInMemory() || DbBaseContext.HasActiveTransaction)
        {
            await action().ConfigureAwait(false);
            return;
        }

        IExecutionStrategy strategy = DbBaseContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction? transaction = await DbBaseContext.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                Muonroi.Core.Abstractions.Response.MVoidMethodResult result = await action().ConfigureAwait(false);

                if (result?.IsOk ?? false)
                {
                    await transaction!.CommitAsync().ConfigureAwait(false);
                }
                else
                {
                    await transaction!.RollbackAsync().ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                await transaction!.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Rolls back the current transaction asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RollbackTransactionAsync()
    {
        IDbContextTransaction? transaction = DbBaseContext.Database.CurrentTransaction;
        if (transaction != null)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Restores a soft-deleted entity asynchronously.
    /// </summary>
    /// <param name="entity">The entity to restore.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the restoration was successful.</returns>
    public async Task<bool> SoftRestoreAsync(T entity)
    {
        if (!entity.IsDeleted)
        {
            return false;
        }

        entity.IsDeleted = false;
        entity.DeletedDateTs = null;
        entity.DeletedUserId = null;

        DateTime utcNow = _dateTimeService.UtcNow();
        entity.LastModificationTimeTs = utcNow.GetTimeStamp(true);
        entity.LastModificationUserId = string.IsNullOrEmpty(_authContext?.CurrentUserGuid)
            ? null
            : CurrentUserGuid;

        entity.AddDomainEvent(new MEntityChangedEvent<T>(entity));
        DbBaseContext.TrackEntity(entity);

        return await DbBaseContext.SaveChangesAsync().ConfigureAwait(false) > 0;
    }

    /// <summary>
    /// Performs a bulk insert of entities asynchronously.
    /// </summary>
    /// <param name="entities">The entities to insert.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entities"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when bulk insert fails.</exception>
    public async Task BulkInsertAsync(IEnumerable<T> entities)
    {
        Protect("bulk_insert");
        MGuard.NotNull(entities);

        try
        {
            List<T> entityList = [.. entities];
            if (entityList.Count == 0)
            {
                return;
            }

            await DbBaseContext.BulkInsertAsync(entityList).ConfigureAwait(false);

            entityList[0].AddDomainEvent(new MEntitiesCreatedEvent<T>(entityList));
            foreach (T entity in entityList)
            {
                DbBaseContext.TrackEntity(entity);
            }
        }
        catch (Exception ex)
        {
            throw new MInternalException("Bulk insert failed.");
        }
    }

    /// <summary>
    /// Executes a stored procedure asynchronously.
    /// </summary>
    /// <param name="storedProcedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters for the stored procedure.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows affected.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="storedProcedureName"/> is null or empty.</exception>
    [Obsolete("Raw SQL bypasses EF tenant query filters. Use the overload with explicit tenantId parameter, or ensure TenantContext.CurrentTenantId is set.")]
    public virtual async Task<int> ExecuteStoredProcedureAsync(string storedProcedureName, params object[] parameters)
    {
        MGuard.NotEmpty(storedProcedureName);

        if (string.IsNullOrEmpty(TenantContext.CurrentTenantId) && !TenantContext.AllowCrossTenantAccess)
        {
            _logger?.LogWarning("[SAFE-10] Raw SQL execution '{StoredProcedureName}' without TenantContext. Set CurrentTenantId or AllowCrossTenantAccess.", storedProcedureName);
        }

        string commandText = $"EXEC {storedProcedureName}";
        return await DbBaseContext.Database
            .ExecuteSqlRawAsync(commandText, parameters)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a stored procedure and returns a scalar value asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="storedProcedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters for the stored procedure.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the scalar value returned by the stored procedure.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="storedProcedureName"/> is null or empty.</exception>
    [Obsolete("Raw SQL bypasses EF tenant query filters. Use the overload with explicit tenantId parameter, or ensure TenantContext.CurrentTenantId is set.")]
    public virtual async Task<TResult> ExecuteStoredProcedureScalarAsync<TResult>(string storedProcedureName,
        params object[]? parameters)
    {
        MGuard.NotEmpty(storedProcedureName);

        if (string.IsNullOrEmpty(TenantContext.CurrentTenantId) && !TenantContext.AllowCrossTenantAccess)
        {
            _logger?.LogWarning("[SAFE-10] Raw SQL scalar execution '{StoredProcedureName}' without TenantContext. Set CurrentTenantId or AllowCrossTenantAccess.", storedProcedureName);
        }

        using System.Data.Common.DbCommand command = DbBaseContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = storedProcedureName;
        command.CommandType = CommandType.StoredProcedure;

        if (parameters != null && parameters.Length != 0)
        {
            foreach (object param in parameters)
            {
                command.Parameters.Add(param);
            }
        }

        if (command.Connection is not null && command.Connection.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }

        object? result = await command.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            return default!;
        }

        return (TResult)Convert.ChangeType(result, typeof(TResult));
    }
}
