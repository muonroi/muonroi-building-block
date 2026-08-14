namespace Muonroi.Data.Abstractions.Repositories;

/// <summary>
/// Repository interface with a relaxed entity constraint.
/// Accepts any class implementing <see cref="IEntityBase"/> without requiring inheritance from <see cref="MEntity"/>.
/// This is additive — <see cref="IMRepository{T}"/> remains unchanged for existing consumers.
/// </summary>
/// <typeparam name="T">The entity type, which must be a class implementing <see cref="IEntityBase"/>.</typeparam>
public interface IMRepositoryBase<T> where T : class, IEntityBase
{
    /// <summary>
    /// Gets the unit of work associated with the repository.
    /// </summary>
    IMUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    T Add(T newEntity);

    /// <summary>
    /// Asynchronously updates an existing entity in the repository.
    /// </summary>
    Task<int> UpdateAsync(T updateEntity);

    /// <summary>
    /// Asynchronously deletes an entity from the repository.
    /// </summary>
    Task<bool> DeleteAsync(T deleteEntity);

    /// <summary>
    /// Asynchronously executes an action within a transaction.
    /// </summary>
    Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action);

    /// <summary>
    /// Asynchronously rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync();

    /// <summary>
    /// Asynchronously adds a batch of entities to the repository.
    /// </summary>
    Task<int> AddBatchAsync(IEnumerable<T> newEntities);

    /// <summary>
    /// Asynchronously adds or updates a batch of entities in the repository.
    /// </summary>
    Task<int> AddOrUpdateBatchAsync(IEnumerable<T> newEntities);

    /// <summary>
    /// Asynchronously updates a batch of entities that satisfy a condition.
    /// </summary>
    Task<int> UpdateBatchAsync(Expression<Func<T, bool>> predicate, Action<T> updateAction);

    /// <summary>
    /// Asynchronously deletes a batch of entities that satisfy a condition.
    /// </summary>
    Task<int> DeleteBatchAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Asynchronously deletes a batch of specified entities.
    /// </summary>
    Task<int> DeleteBatchAsync(IEnumerable<T> deleteEntities);

    /// <summary>
    /// Asynchronously restores a soft-deleted entity.
    /// </summary>
    Task<bool> SoftRestoreAsync(T entity);

    /// <summary>
    /// Asynchronously performs a bulk insert of entities.
    /// </summary>
    Task BulkInsertAsync(IEnumerable<T> entities);

    /// <summary>
    /// Asynchronously executes a stored procedure.
    /// </summary>
    Task<int> ExecuteStoredProcedureAsync(string storedProcedureName, params object[] parameters);

    /// <summary>
    /// Asynchronously executes a stored procedure and returns a scalar result.
    /// </summary>
    Task<TResult> ExecuteStoredProcedureScalarAsync<TResult>(string storedProcedureName, params object[] parameters);
}
