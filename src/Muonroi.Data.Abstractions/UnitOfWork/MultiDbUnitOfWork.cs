namespace Muonroi.Data.Abstractions.UnitOfWork;

/// <summary>
/// Unit of work that manages multiple data contexts.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MultiDbUnitOfWork"/> class with the specified data contexts.
/// </remarks>
/// <param name="contexts">The data contexts to be managed.</param>
public class MultiDbUnitOfWork(params IMDataContext[] contexts) : IDisposable
{
    /// <summary>
    /// Saves changes across all managed contexts within a transaction scope.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the total number of state entries written to the database across all contexts.</returns>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        int result = 0;
        foreach (IMDataContext context in contexts)
        {
            result += await context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Disposes all managed contexts and suppresses finalization.
    /// </summary>
    public void Dispose()
    {
        foreach (IMDataContext context in contexts)
        {
            context.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
