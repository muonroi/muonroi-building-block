namespace Muonroi.Data.Abstractions.Repositories;

public interface IMRepository<T> where T : MEntity
{
    IMUnitOfWork UnitOfWork { get; }

    T Add(T newEntity);

    Task<int> UpdateAsync(T updateEntity);

    Task<bool> DeleteAsync(T deleteEntity);

    Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action);

    Task RollbackTransactionAsync();

    Task<int> AddBatchAsync(IEnumerable<T> newEntities);

    Task<int> AddOrUpdateBatchAsync(IEnumerable<T> newEntities);

    Task<int> UpdateBatchAsync(Expression<Func<T, bool>> predicate, Action<T> updateAction);

    Task<int> DeleteBatchAsync(Expression<Func<T, bool>> predicate);

    Task<int> DeleteBatchAsync(IEnumerable<T> deleteEntities);

    Task<bool> SoftRestoreAsync(T entity);

    Task BulkInsertAsync(IEnumerable<T> entities);

    Task<int> ExecuteStoredProcedureAsync(string storedProcedureName, params object[] parameters);

    Task<TResult> ExecuteStoredProcedureScalarAsync<TResult>(string storedProcedureName, params object[] parameters);
}
