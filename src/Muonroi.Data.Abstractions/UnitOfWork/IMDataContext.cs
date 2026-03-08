namespace Muonroi.Data.Abstractions.UnitOfWork;

/// <summary>
/// Defines the contract for a data context that can save changes.
/// </summary>
public interface IMDataContext : IDisposable
{
    /// <summary>
    /// Asynchronously saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
