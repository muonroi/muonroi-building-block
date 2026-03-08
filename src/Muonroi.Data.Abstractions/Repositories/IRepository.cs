namespace Muonroi.Data.Abstractions.Repositories;

/// <summary>
/// Defines the contract for a repository that manages entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the entity, which must inherit from <see cref="MEntity"/>.</typeparam>
public interface IMRepository<T> where T : MEntity
{
    /// <summary>
    /// Gets the unit of work associated with the repository.
    /// </summary>
    IMUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="newEntity">The entity to add.</param>
    /// <returns>The added entity.</returns>
    T Add(T newEntity);

    /// <summary>
    /// Asynchronously updates an existing entity in the repository.
    /// </summary>
    /// <param name="updateEntity">The entity to update.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> UpdateAsync(T updateEntity);

    /// <summary>
    /// Asynchronously deletes an entity from the repository.
    /// </summary>
    /// <param name="deleteEntity">The entity to delete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the entity was successfully deleted; otherwise, <c>false</c>.</returns>
    Task<bool> DeleteAsync(T deleteEntity);

    /// <summary>
    /// Asynchronously executes an action within a transaction.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action);

    /// <summary>
    /// Asynchronously rolls back the current transaction.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RollbackTransactionAsync();

    /// <summary>
    /// Asynchronously adds a batch of entities to the repository.
    /// </summary>
    /// <param name="newEntities">The collection of entities to add.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> AddBatchAsync(IEnumerable<T> newEntities);

    /// <summary>
    /// Asynchronously adds or updates a batch of entities in the repository.
    /// </summary>
    /// <param name="newEntities">The collection of entities to add or update.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> AddOrUpdateBatchAsync(IEnumerable<T> newEntities);

    /// <summary>
    /// Asynchronously updates a batch of entities that satisfy a condition.
    /// </summary>
    /// <param name="predicate">A function to test each entity for a condition.</param>
    /// <param name="updateAction">An action to perform on each entity to be updated.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> UpdateBatchAsync(Expression<Func<T, bool>> predicate, Action<T> updateAction);

    /// <summary>
    /// Asynchronously deletes a batch of entities that satisfy a condition.
    /// </summary>
    /// <param name="predicate">A function to test each entity for a condition.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> DeleteBatchAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Asynchronously deletes a batch of specified entities.
    /// </summary>
    /// <param name="deleteEntities">The collection of entities to delete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> DeleteBatchAsync(IEnumerable<T> deleteEntities);

    /// <summary>
    /// Asynchronously restores a soft-deleted entity.
    /// </summary>
    /// <param name="entity">The entity to restore.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the entity was successfully restored; otherwise, <c>false</c>.</returns>
    Task<bool> SoftRestoreAsync(T entity);

    /// <summary>
    /// Asynchronously performs a bulk insert of entities.
    /// </summary>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task BulkInsertAsync(IEnumerable<T> entities);

    /// <summary>
    /// Asynchronously executes a stored procedure.
    /// </summary>
    /// <param name="storedProcedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters for the stored procedure.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows affected.</returns>
    Task<int> ExecuteStoredProcedureAsync(string storedProcedureName, params object[] parameters);

    /// <summary>
    /// Asynchronously executes a stored procedure and returns a scalar result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="storedProcedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters for the stored procedure.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the scalar result of the stored procedure.</returns>
    Task<TResult> ExecuteStoredProcedureScalarAsync<TResult>(string storedProcedureName, params object[] parameters);
}
