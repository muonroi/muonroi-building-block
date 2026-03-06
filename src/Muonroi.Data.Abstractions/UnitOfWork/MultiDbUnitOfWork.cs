namespace Muonroi.Data.Abstractions.UnitOfWork;

/// <summary>
/// Unit of work that manages multiple data contexts.
/// </summary>
public class MultiDbUnitOfWork(params IMDataContext[] contexts) : IDisposable
{
    /// <summary>
    /// Saves changes across all managed contexts within a transaction scope.
    /// </summary>
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
