namespace Muonroi.Data.Abstractions.UnitOfWork;

/// <summary>
/// Defines the contract for a unit of work that coordinates the saving of changes across one or more repositories.
/// </summary>
public interface IMUnitOfWork : IDisposable
{
    /// <summary>
    /// Asynchronously saves all changes made in this unit of work to the database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously saves all entities changed in this unit of work to the database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains a unique identifier for the save operation.</returns>
    Task<Guid> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
